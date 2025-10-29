using System.Linq;
using CoherenceOne.Core.Interfaces;
using CoherenceOne.Core.Models;
using CoherenceOne.Data;
using Microsoft.EntityFrameworkCore;

namespace CoherenceOne.Services.Journal;

public class JournalService : IJournalService
{
    private readonly AppDbContext _dbContext;

    public JournalService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(JournalNote note)
    {
        _dbContext.Journal.Add(note);
        _dbContext.SaveChanges();
    }

    public IEnumerable<JournalNote> List(DateOnly date)
    {
        return _dbContext.Journal
            .AsNoTracking()
            .Where(j => j.Date == date)
            .ToList();
    }
}
