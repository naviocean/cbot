using System;
using RedWave.Common;

namespace CommonTests
{
    public static class VolumeProfileTests
    {
        public static void RunAll()
        {
            Test_LegacyVolumeProfile_Instance();
        }

        private static void Test_LegacyVolumeProfile_Instance()
        {
            var vp = new CVolumeProfile();

            TestRunner.Assert(vp != null, "CVolumeProfile (Legacy V1) instance created successfully");
            TestRunner.Assert(vp.LastProfile != null, "CVolumeProfile LastProfile initialized to empty ProfileData");
            TestRunner.Assert(vp.LastProfile.IsValid == false, "CVolumeProfile LastProfile.IsValid is initially false before calculation");
        }
    }
}
