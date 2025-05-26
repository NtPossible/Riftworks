using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Riftworks.src.Config
{
    public class RiftworksConfig
    {
        public static RiftworksConfig Loaded { get; set; } = new RiftworksConfig();
        
        public bool DisableTemporalDisassembler { get; set; } = false;
        public bool DisableStormCaster { get; set; } = false;

        public bool DisableVectorStasisUnit { get; set; } = false;

        public bool DisableDivingHelmet { get; set; } = false;

        //public bool DisableRiftBlade { get; set; } = false;
        //public bool DisableAdaptiveReconstitutionGear { get; set; } = false;
        //public bool DisableOreScanner { get; set; } = false;
        //public bool DisableGravityBoots { get; set; } = false;
        //public bool DisableAdaptiveReconstitutionGearEntity { get; set; } = false;

    }
}
