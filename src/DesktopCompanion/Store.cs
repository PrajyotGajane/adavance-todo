using System.Globalization;

namespace DesktopCompanion;

// All task/note state lives in SQLite; this keeps an in-memory copy for fast UI reads.
public static class Store
{
    static readonly List<TaskItem> _tasks = new();
    static List<NoteItem> _notes = new();

    public static event Action? TasksChanged;
    public static event Action? CountsChanged;
    public static event Action? NotesChanged;

    const string D = "yyyy-MM-dd", T = "HH:mm", DT = "yyyy-MM-dd HH:mm:ss";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static IReadOnlyList<NoteItem> Notes => _notes;

    public static void Init()
    {
        Db.Open();
        Prefs.Load();
        if (Prefs.Get("seeded", "") != "1") { Seed(); Prefs.Set("seeded", "1"); }
        foreach (var r in Db.Query("SELECT id,title,due_date,due_time,tag,done,created FROM tasks"))
        {
            var t = new TaskItem
            {
                Id = (long)r[0]!,
                Title = (string)r[1]!,
                DueDate = r[2] is string d ? DateTime.ParseExact(d, D, Inv) : null,
                DueTime = r[3] is string tm ? TimeSpan.ParseExact(tm, @"hh\:mm", Inv) : null,
                Tag = r[4] as string,
                Created = DateTime.ParseExact((string)r[6]!, DT, Inv),
            };
            t.LoadDone((long)r[5]! != 0);
            _tasks.Add(t);
        }
        LoadNotes();
    }

    static void Seed()
    {
        var today = DateTime.Today;
        void Task(string title, DateTime? date, string? time, string? tag) =>
            Db.Exec("INSERT INTO tasks(title,due_date,due_time,tag,done,created) VALUES(@t,@d,@tm,@g,0,@c)",
                ("@t", title), ("@d", date?.ToString(D, Inv)), ("@tm", time), ("@g", tag), ("@c", DateTime.Now.ToString(DT, Inv)));
        Task("Finish KPIs PR", today, "10:00", "work");
        Task("Reply to email", today, "11:30", "work");
        Task("Workout", today, "18:00", "health");
        Task("Read DSA (Arrays)", today, null, "study");
        Task("Book bike service", today, null, "bike");
        Task("Plan weekend ride", today, null, "bike");
        Task("Call dentist", today.AddDays(2), null, "health");
        Task("Buy new laptop", today.AddDays(6), null, "shopping");
        Task("Update resume", today.AddDays(-3), null, "career");
        Task("Learn Rust basics", null, null, "study");
        Task("Organize photo library", null, null, "home");
        void Note(string body, int daysAgo) =>
            Db.Exec("INSERT INTO notes(body,created,updated) VALUES(@b,@c,@c)",
                ("@b", body), ("@c", DateTime.Now.AddDays(-daysAgo).ToString(DT, Inv)));
        Note("Meeting with Priya\nDiscussed the new dashboard requirements...", 5);
        Note("Ideas for side project\nBuild a lightweight desktop app...", 3);
        Note("API rate limits discussion\nDiscuss API rate limits and caching strategies", 0);
    }

    static void LoadNotes()
    {
        _notes = Db.Query("SELECT id,body,created,updated FROM notes ORDER BY updated DESC, id DESC")
            .Select(r => new NoteItem
            {
                Id = (long)r[0]!,
                Body = (string)r[1]!,
                Created = DateTime.ParseExact((string)r[2]!, DT, Inv),
                Updated = DateTime.ParseExact((string)r[3]!, DT, Inv),
            }).ToList();
    }

    // ---- tasks ----

    public static List<TaskItem> For(Bucket b)
    {
        IEnumerable<TaskItem> q = _tasks.Where(t => b switch
        {
            Bucket.Backlog => t.Bucket == Bucket.Backlog && !t.IsDone,
            _ => t.Bucket == b && !(t.IsDone && t.DueDate is { } d && d.Date < DateTime.Today),
        });
        return q.OrderBy(t => t.IsDone)
                .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
                .ThenBy(t => t.DueTime ?? TimeSpan.MaxValue)
                .ThenBy(t => t.Id).ToList();
    }

    public static int Count(Bucket b) => For(b).Count(t => !t.IsDone);

    public static TaskItem AddTask(string title, DateTime? date, TimeSpan? time, string? tag)
    {
        var created = DateTime.Now;
        var id = Db.Insert("INSERT INTO tasks(title,due_date,due_time,tag,done,created) VALUES(@t,@d,@tm,@g,0,@c)",
            ("@t", title), ("@d", date?.ToString(D, Inv)), ("@tm", time?.ToString(@"hh\:mm", Inv)), ("@g", tag),
            ("@c", created.ToString(DT, Inv)));
        var task = new TaskItem { Id = id, Title = title, DueDate = date, DueTime = time, Tag = tag, Created = created };
        _tasks.Add(task);
        Raise();
        return task;
    }

    public static void UpdateTask(TaskItem t, string title, DateTime? date, TimeSpan? time, string? tag)
    {
        t.Title = title; t.DueDate = date; t.DueTime = time; t.Tag = tag;
        Persist(t);
        Raise();
    }

    static void Persist(TaskItem t) =>
        Db.Exec("UPDATE tasks SET title=@t,due_date=@d,due_time=@tm,tag=@g,done=@x WHERE id=@id",
            ("@t", t.Title), ("@d", t.DueDate?.ToString(D, Inv)), ("@tm", t.DueTime?.ToString(@"hh\:mm", Inv)),
            ("@g", t.Tag), ("@x", t.IsDone ? 1 : 0), ("@id", t.Id));

    public static void SetDone(TaskItem t, bool done)
    {
        Db.Exec("UPDATE tasks SET done=@x WHERE id=@id", ("@x", done ? 1 : 0), ("@id", t.Id));
        CountsChanged?.Invoke();
    }

    public static void DeleteTask(TaskItem t)
    {
        Db.Exec("DELETE FROM tasks WHERE id=@id", ("@id", t.Id));
        _tasks.Remove(t);
        Raise();
    }

    public static void Move(TaskItem t, Bucket to)
    {
        var today = DateTime.Today;
        switch (to)
        {
            case Bucket.Today: t.DueDate = today; break;
            case Bucket.Upcoming: if (t.DueDate is not { } u || u.Date <= today) t.DueDate = today.AddDays(1); break;
            case Bucket.Backlog:
                if (t.DueDate is not { } b || b.Date >= today) t.DueDate = today.AddDays(-1);
                t.IsDone = false;
                break;
            case Bucket.Later: t.DueDate = null; t.DueTime = null; break;
        }
        Persist(t);
        Raise();
    }

    static void Raise() { TasksChanged?.Invoke(); CountsChanged?.Invoke(); }

    // ---- notes ----

    public static NoteItem AddNote(string body)
    {
        var now = DateTime.Now;
        var id = Db.Insert("INSERT INTO notes(body,created,updated) VALUES(@b,@c,@c)",
            ("@b", body), ("@c", now.ToString(DT, Inv)));
        var n = new NoteItem { Id = id, Body = body, Created = now, Updated = now };
        _notes = _notes.Prepend(n).ToList();
        NotesChanged?.Invoke();
        return n;
    }

    public static void UpdateNote(NoteItem n, string body)
    {
        if (n.Body == body) return;
        n.Body = body;
        n.Updated = DateTime.Now;
        Db.Exec("UPDATE notes SET body=@b, updated=@u WHERE id=@id",
            ("@b", body), ("@u", n.Updated.ToString(DT, Inv)), ("@id", n.Id));
        _notes = _notes.OrderByDescending(x => x.Updated).ThenByDescending(x => x.Id).ToList();
        NotesChanged?.Invoke();
    }

    public static void DeleteNote(NoteItem n)
    {
        Db.Exec("DELETE FROM notes WHERE id=@id", ("@id", n.Id));
        _notes = _notes.Where(x => x.Id != n.Id).ToList();
        NotesChanged?.Invoke();
    }
}
