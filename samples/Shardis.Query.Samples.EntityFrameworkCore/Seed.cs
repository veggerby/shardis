namespace Shardis.Query.Samples.EntityFrameworkCore;

public static class Seed
{
    public static void Ensure(PersonContext ctx, IEnumerable<Person> people)
    {
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        if (!ctx.People.Any())
        {
            ctx.People.AddRange(people);
            ctx.SaveChanges();
        }
    }
}