#:package LiteDB@5.0.11
// Run only to produce the disposable compatibility fixture. Never point at application data.
using LiteDB;

if (args.Length != 1 || File.Exists(args[0]))
    throw new ArgumentException("Supply a new output database filename; existing files are never overwritten.");

using (var database = new LiteDatabase(args[0]))
{
    var items = database.GetCollection<ToDoItem>(nameof(ToDoItem));
    items.Insert(new ToDoItem
    {
        ToDoItemId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Description = "Legacy café ✓",
        IsCompleted = true,
        CreatedDate = new DateTime(2021, 2, 4, 12, 30, 45, DateTimeKind.Utc)
    });
    items.Insert(new ToDoItem
    {
        ToDoItemId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Description = " preserved legacy text ",
        IsCompleted = false,
        CreatedDate = new DateTime(2021, 2, 3, 10, 20, 30, DateTimeKind.Utc)
    });
}
Console.WriteLine($"Created with {typeof(LiteDatabase).Assembly.FullName}");
Console.WriteLine(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(args[0]))));

// Matches the baseline persisted model and collection name. LiteDB maps ToDoItemId to _id.
public record ToDoItem
{
    public Guid ToDoItemId { get; init; }
    public string Description { get; init; } = "";
    public bool IsCompleted { get; init; }
    public DateTime CreatedDate { get; init; }
}
