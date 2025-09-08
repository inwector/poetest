namespace PoETest.API.Models
{
    public class Item
    {
        public int Id { get; set; }
        public int TypeId { get; set; }
        public int Fame { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Link { get; set; }

        public ItemType Type { get; set; } = null!;
        public ICollection<Modifier> Modifiers { get; set; } = new List<Modifier>();
    }
}