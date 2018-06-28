using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tomware.Microwf.Engine
{
  [Table("Workflow")]
  public partial class Workflow
  {
    [Key]
    public int Id { get; set; }

    [Required]
    public string Type { get; set; }

    [Required]
    public string State { get; set; }

    [Required]
    public int CorrelationId { get; set; }

    public string Assignee { get; set; }

    public DateTime? DueDate { get; set; }

    public static Workflow Create(
      int correlationId,
      string type,
      string state,
      string assignee,
      DateTime? dueDate = null
    )
    {
      return new Workflow
      {
        CorrelationId = correlationId,
        Type = type,
        State = state,
        Assignee = assignee,
        DueDate = dueDate
      };
    }
  }
}
