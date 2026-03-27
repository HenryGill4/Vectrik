using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

public class WorkOrderComment
{
    public int Id { get; set; }

    [Required]
    public int WorkOrderId { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [MaxLength(100)]
    public string AuthorName { get; set; } = string.Empty;

    public int? AuthorUserId { get; set; }

    public int? ParentCommentId { get; set; }

    public bool IsInternal { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? EditedDate { get; set; }

    // Navigation
    public virtual WorkOrder WorkOrder { get; set; } = null!;
    public virtual User? AuthorUser { get; set; }
    public virtual WorkOrderComment? ParentComment { get; set; }
    public virtual ICollection<WorkOrderComment> Replies { get; set; } = new List<WorkOrderComment>();
}
