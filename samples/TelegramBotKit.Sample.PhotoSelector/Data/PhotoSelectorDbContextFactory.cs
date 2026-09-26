using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TelegramBotKit.Sample.PhotoSelector.Data;

public sealed class PhotoSelectorDbContextFactory : IDesignTimeDbContextFactory<PhotoSelectorDbContext>
{
    public PhotoSelectorDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PhotoSelectorDbContext>()
            .UseSqlite("Data Source=data/photo-selector.db")
            .Options;

        return new PhotoSelectorDbContext(options);
    }
}
