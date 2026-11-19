namespace MedGas2024.Core
{
    public sealed class RoomReviewRow
    {
        public bool CanPlace{get;set;}public string OldName{get;set;}public string NewName{get;set;}
        public string Handle{get;set;}public string Id{get;set;}public string Current{get;set;}public string After{get;set;}public string Status{get;set;}
        public string Action{get;set;}public string Layer{get;set;}public bool Ready{get;set;}
    }
    // Choices shown above the layer review; the command re-matches the drawing when they change.
    public sealed class RoomReviewChoices
    {
        public bool CreateLabels=true,AllowRepeats,Mask,HideSource,KeepUnmatched;
        // Set by the command: the source layer may be turned off only when every
        // label is made for text stored on that layer (never layer 0 or inherited).
        public string HideLayer;public bool CanHide;
        public RoomReviewChoices Copy(){return (RoomReviewChoices)MemberwiseClone();}
    }
}
namespace MedGas2024.Core
{
    public sealed class RoomReviewPage
    {
        public System.Collections.Generic.List<RoomReviewRow> Rows;public string Summary;
    }
}
