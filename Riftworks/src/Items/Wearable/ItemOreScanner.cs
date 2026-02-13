namespace Riftworks.src.Items.Wearable
{
    public class ItemOreScanner : FuelWearable
    {
        protected override float FuelHoursCapacity => 24f;
        protected override string MergeErrorItemName => "orescanner";
    }
}
