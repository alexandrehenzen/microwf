using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using tomware.Microwf.Core;

namespace tomware.Microwf.Engine
{
  public interface IWorkflowService
  {
    /// <summary>
    /// Returns a list of workflow instances.
    /// </summary>
    /// <param name="pagingParameters"></param>
    /// <returns></returns>
    Task<PaginatedList<WorkflowViewModel>> GetWorkflowsAsync(PagingParameters pagingParameters);

    /// <summary>
    /// Returns a workflow instance.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<WorkflowViewModel> GetAsync(int id);

    /// <summary>
    /// Returns the workflow history for a workflow instance.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<IEnumerable<WorkflowHistory>> GetHistoryAsync(int id);

    /// <summary>
    /// Returns the workflow variables for a workflow instance.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<IEnumerable<WorkflowVariable>> GetVariablesAsync(int id);

    /// <summary>
    /// Returns a workflow instance.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    Task<WorkflowViewModel> GetInstanceAsync(string type, int correlationId);

    /// <summary>
    /// Returns a list of workflow definitions that exist in the system.
    /// </summary>
    /// <returns></returns>
    IEnumerable<WorkflowDefinitionViewModel> GetWorkflowDefinitions();

    /// <summary>
    /// Returns the dot -> diagraph notation for the given workflow type. 
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    string Dot(string type);

    /// <summary>
    /// Returns the dot -> diagraph notation for the given workflow type with history information.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    Task<string> DotWithHistoryAsync(string type, int correlationId);
  }

  public class WorkflowService<TContext> : IWorkflowService where TContext : EngineDbContext
  {
    private readonly EngineDbContext _context;
    private readonly ILogger<WorkflowService<TContext>> _logger;
    private readonly IWorkflowDefinitionProvider _workflowDefinitionProvider;
    private readonly IUserWorkflowMappingService _userWorkflowMappingService;
    private readonly IWorkflowDefinitionViewModelCreator _viewModelCreator;
    private readonly IUserContextService _userContext;

    public WorkflowService(
      TContext context,
      ILogger<WorkflowService<TContext>> logger,
      IWorkflowDefinitionProvider workflowDefinitionProvider,
      IUserWorkflowMappingService userWorkflowMappingService,
      IWorkflowDefinitionViewModelCreator viewModelCreator,
      IUserContextService userContext
    )
    {
      _context = context ?? throw new ArgumentNullException(nameof(context));
      _logger = logger;
      _workflowDefinitionProvider = workflowDefinitionProvider;
      _userWorkflowMappingService = userWorkflowMappingService;
      _viewModelCreator = viewModelCreator;
      _userContext = userContext;
    }

    public async Task<PaginatedList<WorkflowViewModel>> GetWorkflowsAsync(
      PagingParameters pagingParameters
    )
    {
      var count = _context.Workflows.Count();

      var instances = await _context.Workflows
        .OrderByDescending(w => w.Id)
        .Skip(pagingParameters.SkipCount)
        .Take(pagingParameters.PageSize)
        .ToListAsync();

      var items = instances.Select(i => ToWorkflowViewModel(i));

      return new PaginatedList<WorkflowViewModel>(
        items,
        count,
        pagingParameters.PageIndex,
        pagingParameters.PageSize
      );
    }

    public async Task<WorkflowViewModel> GetAsync(int id)
    {
      var workflow = await _context.Workflows.FindAsync(id);
      if (workflow == null) throw new KeyNotFoundException(nameof(id));

      return ToWorkflowViewModel(workflow);
    }

    public async Task<IEnumerable<WorkflowHistory>> GetHistoryAsync(int id)
    {
      var workflow = await _context.Workflows
        .Include(h => h.WorkflowHistories)
        .Where(w => w.Id == id).FirstAsync();
      if (workflow == null) throw new KeyNotFoundException(nameof(id));

      return workflow.WorkflowHistories.OrderByDescending(h => h.Created);
    }

    public async Task<IEnumerable<WorkflowVariable>> GetVariablesAsync(int id)
    {
      var workflow = await _context.Workflows
       .Include(v => v.WorkflowVariables)
       .Where(w => w.Id == id).FirstAsync();
      if (workflow == null) throw new KeyNotFoundException(nameof(id));

      return workflow.WorkflowVariables.OrderBy(v => v.Type);
    }

    public async Task<WorkflowViewModel> GetInstanceAsync(string type, int correlationId)
    {
      var workflow = await _context.Workflows
        .FirstAsync(w => w.Type == type && w.CorrelationId == correlationId);
      if (workflow == null) throw new KeyNotFoundException($"{type}, {correlationId}");

      return ToWorkflowViewModel(workflow);
    }

    public IEnumerable<WorkflowDefinitionViewModel> GetWorkflowDefinitions()
    {
      var workflowDefinitions = _workflowDefinitionProvider.GetWorkflowDefinitions();

      workflowDefinitions = _userWorkflowMappingService.Filter(workflowDefinitions);

      return workflowDefinitions.Select(d => _viewModelCreator.CreateViewModel(d.Type));
    }

    public string Dot(string type)
    {
      if (string.IsNullOrEmpty(type)) throw new ArgumentNullException(nameof(type));

      var workflowDefinition = _workflowDefinitionProvider.GetWorkflowDefinition(type);

      return workflowDefinition.ToDot();
    }

    public async Task<string> DotWithHistoryAsync(string type, int correlationId)
    {
      if (string.IsNullOrEmpty(type)) throw new ArgumentNullException(nameof(type));

      var workflow = await _context.Workflows
        .Include(h => h.WorkflowHistories)
        .FirstAsync(w => w.Type == type && w.CorrelationId == correlationId);
      if (workflow == null) throw new KeyNotFoundException($"{type}, {correlationId}");

      var workflowDefinition = _workflowDefinitionProvider.GetWorkflowDefinition(type);

      return workflowDefinition.ToDotWithHistory(workflow);
    }

    private WorkflowViewModel ToWorkflowViewModel(Workflow w)
    {
      var model = _viewModelCreator.CreateViewModel(w.Type);

      return new WorkflowViewModel
      {
        Id = w.Id,
        CorrelationId = w.CorrelationId,
        Type = w.Type,
        State = w.State,
        Title = model.Title,
        Description = model.Description,
        Assignee = w.Assignee,
        Started = w.Started,
        Completed = w.Completed,
        Route = model.Route
      };
    }
  }
}
