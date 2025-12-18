using Microsoft.EntityFrameworkCore;

namespace Superchef.Services;

public class GenerateSlotService
{
    private readonly DB db;

    public GenerateSlotService(DB db)
    {
        this.db = db;
    }

    public void InitializeSlots()
    {
        var lastGeneration = db.AuditLogs
            .Where(a => a.Action == "generate-slot")
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();
        DateTime lastGenerationDate = lastGeneration?.CreatedAt ?? DateTime.Now.AddDays(-6);

        if (DateOnly.FromDateTime(lastGenerationDate) < DateOnly.FromDateTime(DateTime.Today))
        {
            StartSlotGeneration(lastGenerationDate);
        }
    }

    public void StartSlotGeneration(DateTime date)
    {
        while (DateOnly.FromDateTime(date) < DateOnly.FromDateTime(DateTime.Today))
        {
            var futureDate = date.AddDays(6);

            var stores = db.Stores
                .Include(s => s.SlotTemplates)
                .Where(s => !s.IsDeleted && s.HasPublishedFirstSlots)
                .ToList();

            foreach (var store in stores)
            {
                if (db.Slots.Any(s => 
                    s.StoreId == store.Id && 
                    DateOnly.FromDateTime(s.StartTime) == DateOnly.FromDateTime(futureDate)
                ))
                {
                    continue;
                }

                foreach (var template in store.SlotTemplates.Where(t => t.DayOfWeek == (int)futureDate.DayOfWeek))
                {
                    var startDateTime = new DateTime(futureDate.Year, futureDate.Month, futureDate.Day, template.StartTime.Hour, template.StartTime.Minute, 0);

                    db.Slots.Add(new()
                    {
                        StartTime = startDateTime,
                        EndTime = startDateTime.AddMinutes(30),
                        MaxOrders = store.SlotMaxOrders,
                        StoreId = store.Id
                    });
                }
            }
            db.SaveChanges();

            date = date.AddDays(1);
        }

        db.AuditLogs.Add(new()
        {
            Action = "generate-slot",
            AccountId = 1,
        });
        db.SaveChanges();
    }
}