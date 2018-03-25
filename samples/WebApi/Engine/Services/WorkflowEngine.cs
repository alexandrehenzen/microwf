using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using tomware.Microwf.Core;
using WebApi.Domain;

namespace tomware.Microwf.Engine
{
  public interface IWorkflowEngine
  {
    IEnumerable<TriggerResult> GetTriggers(
      IWorkflow instance,
      Dictionary<string, WorkflowVariableBase> variables = null
    );

    TriggerResult CanTrigger(TriggerParam param);

    TriggerResult Trigger(TriggerParam param);
  }

  public class WorkflowEngine<TContext> : IWorkflowEngine where TContext : DbContext
  {
    private readonly DomainContext _context;
    private readonly ILogger<WorkflowEngine<TContext>> _logger;
    private readonly IWorkflowDefinitionProvider _workflowDefinitionProvider;

    public WorkflowEngine(
      DomainContext context,
      ILoggerFactory loggerFactory,
      IWorkflowDefinitionProvider workflowDefinitionProvider
    )
    {
      this._context = context ?? throw new ArgumentNullException(nameof(context));
      this._logger = loggerFactory.CreateLogger<WorkflowEngine<TContext>>();
      this._workflowDefinitionProvider = workflowDefinitionProvider;
    }

    public TriggerResult CanTrigger(TriggerParam param)
    {
      if (param == null) throw new InvalidOperationException(nameof(param));

      var execution = GetExecution(param.Instance.Type);

      return execution.CanTrigger(param);
    }

    public IEnumerable<TriggerResult> GetTriggers(
      IWorkflow instance,
      Dictionary<string, WorkflowVariableBase> variables = null
    )
    {
      if (instance == null) throw new InvalidOperationException(nameof(instance));

      var execution = GetExecution(instance.Type);

      return execution.GetTriggers(instance, variables);
    }

    public TriggerResult Trigger(TriggerParam param)
    {
      if (param == null) throw new InvalidOperationException(nameof(param));

      TriggerResult result = null;

      using (var transaction = this._context.Database.BeginTransaction())
      {
        int? id = null;
        try
        {
          var execution = GetExecution(param.Instance.Type);

          var workflow = param.Instance as IEntityWorkflow;
          WorkflowContext workflowContext = null;
          if (workflow != null)
          {
            id = workflow.Id;
            workflowContext = FindOrCreate(id.Value, param);
          }

          result = execution.Trigger(param);
          if (!result.IsAborted)
          {
            if (id.HasValue && param.HasVariables)
            {
              this.PersistContext(workflowContext, param.Variables);
            }

            this._context.SaveChanges();
            transaction.Commit();
          }
        }
        catch (Exception ex)
        {
          transaction.Rollback();

          this._logger.LogError($"Error in triggering: {param.Instance.Type}, EntityId: {id}", ex);
        }
      }

      return result;
    }

    private WorkflowExecution GetExecution(string type)
    {
      var definition = this._workflowDefinitionProvider.GetWorkflowDefinition(type);

      return new WorkflowExecution(definition);
    }

    private WorkflowContext FindOrCreate(int id, TriggerParam triggerParam)
    {
      var ctx = this._context.Workflows.SingleOrDefault(w => w.CorrelationId == id);
      if (ctx == null)
      {
        ctx = WorkflowContext.Create(id, triggerParam.Instance.Type);
        this._context.Add(ctx);
      }

      if (!triggerParam.HasVariables && !string.IsNullOrWhiteSpace(ctx.Context))
      {
        var variables = JsonConvert
          .DeserializeObject<Dictionary<string, WorkflowVariableBase>>(ctx.Context);
        foreach (var variable in variables)
        {
          triggerParam.Variables.Add(variable.Key, variable.Value);
        }
      }

      return ctx;
    }

    private void PersistContext(
      WorkflowContext workflowContext,
      Dictionary<string, WorkflowVariableBase> variables,
      DateTime? dueDate = null
    )
    {
      if (workflowContext == null) throw new ArgumentNullException(nameof(workflowContext));

      string context = null;
      if (variables != null)
      {
        context = JsonConvert.SerializeObject(variables);
        workflowContext.Context = context;
      }

      workflowContext.DueDate = dueDate;
    }
  }
}