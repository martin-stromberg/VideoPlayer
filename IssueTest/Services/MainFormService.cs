using IssueTest.DataStore;
using IssueTest.Model;
using IssueTest.ViewModels;

namespace IssueTest.Services
{
    internal class MainFormService
    {
        private Database dataStore;

        public MainFormService()
        {
            dataStore = new Database();
        }

        internal async Task<IEnumerable<Item>> GetItems()
        {
            var items = await (await dataStore.GetItemsAsync())
                .ToArrayAsync();
            return await MainThread.InvokeOnMainThreadAsync(() => {
                return items.Select(item => new Item()
                {
                    Id = item.Id,
                    Name = item.Name
                });
            });
        }

        internal async Task InitAsync()
        {
            for (var i = 0; i < 10; i++)
                await dataStore.AddItemAsync(new DataStore.Model.Item()
                {
                    Name = $"Entry {i}"
                });
        }

        internal async Task<bool> IsEmptyAsync()
        {
            return await (await dataStore.GetItemsAsync()).FirstOrDefaultAsync() == null;
        }
    }
}