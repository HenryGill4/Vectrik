# Module 18: User Training & Knowledge Management (LMS)

## Status: [ ] Not Started
## Category: QMS
## Phase: 3 — Platform Maturity
## Priority: P3 - Medium

---

## Overview

The integrated Learning Management System (LMS) solves ProShop's most-cited
weakness: steep learning curve. It provides role-based learning paths, interactive
software tutorials, skill competency tracking, and a tribal knowledge capture
system where experienced operators record tips tied to specific parts or operations.

**ProShop Improvements**: Role-based learning paths (not one-size-fits-all),
interactive in-app tutorials, skill matrices with competency assessments,
contextual help on every screen, and a tribal knowledge system where operator
expertise is captured and surfaced automatically during relevant work.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `OperatorSkill` model (from M13) | ✅ M13 | `Models/OperatorSkill.cs` |
| `User.Role` assignments | ✅ Exists | `Models/User.cs` |
| `WorkInstruction` feedback model (from M03) | ✅ M03 | `Models/OperatorFeedback.cs` |

**Gap**: No LMS model, no training records, no learning paths, no tribal knowledge model, no in-app contextual help system.

---

## What Needs to Be Built

### 1. Database Models (New)
- `TrainingCourse` — a training course or module
- `TrainingLesson` — individual lessons within a course
- `TrainingEnrollment` — user enrolled in a course
- `TrainingCompletion` — lesson/course completion record
- `KnowledgeArticle` — tribal knowledge entries linked to parts/operations

### 2. Service Layer (New)
- `TrainingService` — enrollment, progress tracking, completion management
- `KnowledgeBaseService` — tribal knowledge articles, contextual retrieval

### 3. UI Components (New)
- **My Training** — operator's learning dashboard with progress
- **Course Viewer** — lesson content with quiz/completion
- **Training Admin** — create courses, assign to roles
- **Knowledge Base** — searchable tribal knowledge articles
- **Contextual Help** — help button on every page

---

## Implementation Steps

### Step 1 — Create TrainingCourse Model
**New File**: `Models/TrainingCourse.cs`
```csharp
public class TrainingCourse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CourseType CourseType { get; set; }                 // SoftwareTraining, SafetyTraining, ProcessTraining
    public List<string> TargetRoles { get; set; } = new();    // JSON: ["Operator", "Technician"]
    public bool IsRequired { get; set; } = false;
    public int? RequiredRenewalDays { get; set; }              // How often re-cert required
    public int EstimatedMinutes { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public ICollection<TrainingLesson> Lessons { get; set; } = new List<TrainingLesson>();
}

public enum CourseType { SoftwareTraining, SafetyTraining, ProcessTraining, QualityTraining, Onboarding }
```

### Step 2 — Create TrainingLesson Model
**New File**: `Models/TrainingLesson.cs`
```csharp
public class TrainingLesson
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public TrainingCourse Course { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }                       // Markdown/HTML lesson content
    public string? VideoUrl { get; set; }                      // Embedded video
    public LessonType LessonType { get; set; }                 // Text, Video, Quiz, Interactive
    public int DisplayOrder { get; set; }
    public string? QuizQuestionsJson { get; set; }             // JSON array of quiz questions
    public int? PassingScorePct { get; set; }                  // Min score to pass (for quizzes)
    public int EstimatedMinutes { get; set; }
}

public enum LessonType { Text, Video, Quiz, Interactive, Document }
```

### Step 3 — Create TrainingEnrollment Model
**New File**: `Models/TrainingEnrollment.cs`
```csharp
public class TrainingEnrollment
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public int CourseId { get; set; }
    public TrainingCourse Course { get; set; } = null!;
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.NotStarted;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }                   // If renewal required
    public decimal ProgressPct { get; set; }
    public ICollection<TrainingCompletion> Completions { get; set; } = new List<TrainingCompletion>();
}

public enum EnrollmentStatus { NotStarted, InProgress, Completed, Expired, Failed }
```

### Step 4 — Create TrainingCompletion Model
**New File**: `Models/TrainingCompletion.cs`
```csharp
public class TrainingCompletion
{
    public int Id { get; set; }
    public int EnrollmentId { get; set; }
    public TrainingEnrollment Enrollment { get; set; } = null!;
    public int LessonId { get; set; }
    public TrainingLesson Lesson { get; set; } = null!;
    public bool IsCompleted { get; set; }
    public int? QuizScore { get; set; }                        // % score if quiz
    public bool PassedQuiz { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public int? TimeSpentMinutes { get; set; }
}
```

### Step 5 — Create KnowledgeArticle Model
**New File**: `Models/KnowledgeArticle.cs`
```csharp
public class KnowledgeArticle
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;           // Markdown content
    public KnowledgeArticleType ArticleType { get; set; }      // Tip, LessonLearned, Procedure, Warning
    public int? RelatedPartId { get; set; }
    public Part? RelatedPart { get; set; }
    public int? RelatedStageId { get; set; }
    public ProductionStage? RelatedStage { get; set; }
    public int? RelatedMachineId { get; set; }
    public Machine? RelatedMachine { get; set; }
    public int? RelatedMaterialId { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public bool IsApproved { get; set; } = false;              // Reviewed before publication
    public int HelpfulVotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum KnowledgeArticleType { Tip, LessonLearned, Procedure, Warning, BestPractice }
```

### Step 6 — Register DbSets
**File**: `Data/TenantDbContext.cs`
```csharp
public DbSet<TrainingCourse> TrainingCourses { get; set; }
public DbSet<TrainingLesson> TrainingLessons { get; set; }
public DbSet<TrainingEnrollment> TrainingEnrollments { get; set; }
public DbSet<TrainingCompletion> TrainingCompletions { get; set; }
public DbSet<KnowledgeArticle> KnowledgeArticles { get; set; }
```

### Step 7 — Create TrainingService
**New File**: `Services/TrainingService.cs`
**New File**: `Services/ITrainingService.cs`

```csharp
public interface ITrainingService
{
    Task<List<TrainingCourse>> GetCoursesForRoleAsync(string role, string tenantCode);
    Task<List<TrainingEnrollment>> GetMyEnrollmentsAsync(string userId, string tenantCode);
    Task<TrainingEnrollment> EnrollAsync(string userId, int courseId, string tenantCode);
    Task CompleteLesson(int enrollmentId, int lessonId, int? quizScore, string tenantCode);
    Task<TrainingComplianceSummary> GetUserComplianceSummaryAsync(string userId, string tenantCode);
    Task<List<TrainingEnrollment>> GetExpiredOrDueSoonAsync(string tenantCode);
    Task AutoEnrollByRoleAsync(string userId, string role, string tenantCode);  // On user creation
}

public record TrainingComplianceSummary(
    string UserId,
    string UserName,
    int RequiredCourses,
    int Completed,
    int InProgress,
    int Overdue,
    decimal CompliancePct
);
```

**Auto-enrollment rule**: When a user is created (or role changed), automatically enroll them in all required courses for their role.

### Step 8 — Create KnowledgeBaseService
**New File**: `Services/KnowledgeBaseService.cs`
**New File**: `Services/IKnowledgeBaseService.cs`

```csharp
public interface IKnowledgeBaseService
{
    Task<List<KnowledgeArticle>> SearchAsync(string query, string tenantCode);
    Task<List<KnowledgeArticle>> GetContextualArticlesAsync(int? partId, int? stageId,
                                                              int? machineId, string tenantCode);
    Task<KnowledgeArticle> CreateArticleAsync(KnowledgeArticle article, string tenantCode);
    Task ApproveArticleAsync(int articleId, string tenantCode);
    Task<KnowledgeArticle> VoteHelpfulAsync(int articleId, string userId, string tenantCode);
}
```

**Contextual retrieval**: `GetContextualArticlesAsync` is called from:
- Shop floor stage views (pass `partId` + `stageId`)
- Machine detail page (pass `machineId`)
- Part detail page (pass `partId`)

Returns top 3 most relevant approved articles sorted by helpful votes.

### Step 9 — My Training Dashboard
**New File**: `Components/Pages/Training/MyTraining.razor`
**Route**: `/training/my`

UI requirements:
- **Welcome panel**: "Hello [Name] — you have X courses to complete"
- **Required Courses** (overdue highlighted red): course name, due date, "Start" button
- **In Progress** courses with progress bar
- **Completed** courses with completion date badge
- **Upcoming Renewals**: courses expiring in next 60 days

### Step 10 — Course Viewer
**New File**: `Components/Pages/Training/CourseViewer.razor`
**Route**: `/training/courses/{id:int}`

UI requirements:
- Left sidebar: lesson list with checkmarks for completed lessons
- Main area: lesson content (Markdown rendered, video embed, or quiz)
- **Quiz Mode**: multiple choice questions one at a time with score at end
  - Simple quiz format: `{ question: "...", options: ["A","B","C","D"], answer: "B" }`
  - Show score on completion, pass/fail indicator
- "Next Lesson" button
- Progress saved on each lesson completion
- Completion certificate shown when all lessons done

### Step 11 — Knowledge Base Pages
**New File**: `Components/Pages/Training/KnowledgeBase.razor`
**Route**: `/knowledge`

UI requirements:
- Search bar (full text search across all approved articles)
- Filter: by article type, by part, by stage
- Article cards: title, type badge, related part/stage, author, helpful votes
- Click → article detail with full content and "👍 Helpful" button

**New File**: `Components/Pages/Training/WriteArticle.razor`
**Route**: `/knowledge/write`

UI requirements:
- Title input
- Article type selector
- Body (Markdown editor with preview toggle)
- Link to Part (optional), Stage (optional), Machine (optional)
- "Submit for Review" button → sets `IsApproved = false`
- Admin/Manager: "Publish Directly" button

### Step 12 — Contextual Help Button
**File**: `Components/Layout/MainLayout.razor`

Add a floating "?" button in the corner of every page:
```razor
<button class="help-btn" @onclick="OpenContextualHelp">?</button>

@if (showHelp)
{
    <div class="help-panel">
        <h3>Knowledge Base — @currentPageName</h3>
        @foreach (var article in contextualArticles)
        {
            <div class="help-article">
                <span class="badge">@article.ArticleType</span>
                <strong>@article.Title</strong>
                <p>@article.Body[..Math.Min(150, article.Body.Length)]...</p>
            </div>
        }
        <a href="/knowledge">View All Articles →</a>
    </div>
}
```

Use `NavigationManager.Uri` to determine current page context for fetching relevant articles.

### Step 13 — Training Admin Page
**New File**: `Components/Pages/Admin/TrainingAdmin.razor`
**Route**: `/admin/training`

UI requirements:
- **Courses** tab: list of all courses with "New Course" button and edit/delete per row
- **Team Compliance** tab: table of all users with required/completed/overdue counts
  - "Assign Course" button to manually enroll a user
  - Export compliance report as CSV

### Step 14 — EF Core Migration
```bash
dotnet ef migrations add AddTrainingAndKnowledge --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Training courses can be created with lessons (text, video, quiz)
- [ ] Users auto-enrolled in required courses for their role on creation
- [ ] Lesson completion tracked per user with quiz scores
- [ ] Course shows completion certificate after all lessons done
- [ ] Knowledge articles can be written by any operator
- [ ] Articles require admin approval before publishing
- [ ] Contextual articles surface on shop floor stage views
- [ ] Helpful vote count sorts most useful articles to top
- [ ] Training compliance summary shows overdue users
- [ ] Contextual help "?" button shows page-relevant knowledge articles

---

## Dependencies

- **Module 03** (Visual Work Instructions) — Work instructions can be linked as training content
- **Module 04** (Shop Floor) — Contextual articles surface during stage work
- **Module 13** (Time Clock) — Skill certifications managed alongside training
- **Module 17** (Compliance) — Training completion evidence for CMMC/AS9100

---

## Future Enhancements (Post-MVP)

- Video recording from tablet camera for operator-recorded tribal knowledge
- AI-powered search: "How do I set up a Ti-6Al-4V SLS build?"
- Learning path builder: structured onboarding sequence per department
- External certification upload: attach third-party cert PDFs to skill records
- Training hours reporting for ITAR/AS9100 personnel qualification records
