namespace EkstaziSharp.Model
{
    public interface IDocumentReference
    {
        string Document { get; set; }

        uint FileId { get; set; }
    }
}