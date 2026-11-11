#:package LiteDB@5.0.11
using LiteDB;

if (args.Length != 1 || File.Exists(args[0]) || File.Exists(args[0] + ".json"))
    throw new ArgumentException("Supply a new synthetic database filename; existing files are never overwritten.");

// Exactly the baseline Clock pattern: construct an unspecified DateTime from UTC ticks.
var source = new DateTime(new DateTime(2021, 2, 5, 12, 30, 45, DateTimeKind.Utc).Ticks);
var id = Guid.Parse("33333333-3333-3333-3333-333333333333");
DateTime storedUtc;
using (var database = new LiteDatabase(args[0]))
{
    database.GetCollection<ToDoItem>(nameof(ToDoItem)).Insert(new ToDoItem
    {
        ToDoItemId = id,
        Description = "Legacy unspecified time",
        CreatedDate = source
    });
    storedUtc = database.GetCollection<BsonDocument>(nameof(ToDoItem)).FindById(id)["CreatedDate"].AsDateTime.ToUniversalTime();
    if (storedUtc != source.ToUniversalTime()) throw new InvalidOperationException("Unexpected old-date mapping.");
}
var bytes = File.ReadAllBytes(args[0]);
using (var stream = File.Create(args[0] + ".json"))
using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true }))
{
    writer.WriteStartObject();
    writer.WriteString("assembly", typeof(LiteDatabase).Assembly.FullName);
    writer.WriteString("sourceUnspecified", source.ToString("O"));
    writer.WriteString("sourceTimeZone", TimeZoneInfo.Local.Id);
    writer.WriteString("storedUtc", storedUtc.ToString("O"));
    writer.WriteString("databaseSha256", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
    writer.WriteEndObject();
}
Console.WriteLine(File.ReadAllText(args[0] + ".json"));

public record ToDoItem
{
    public Guid ToDoItemId { get; init; }
    public string Description { get; init; } = "";
    public bool IsCompleted { get; init; }
    public DateTime CreatedDate { get; init; }
}
