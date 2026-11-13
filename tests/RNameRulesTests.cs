using System;
using MedGas2024.Core;
class RNameRulesTests
{
    static void Check(bool x,string name){if(!x)throw new Exception(name);Console.WriteLine("PASS: "+name);}
    static void Main(){
        Check(RNameRules.CanonicalLayer("Mr.T_OXY_D12_RAW")&&!RNameRules.CanonicalLayer("Mr.T Oxy D12 Bổ sung"),"Recognizes a standard Mr.T layer and distinguishes legacy layers");
        Check(RNameRules.LabelKey("Oxy.D12")==RNameRules.LabelKey("D12.Oxy"),"Legacy and modern Oxy D12 labels identify the same family");
        Check(RNameRules.OldName("Mr.T_R0_7ad24ed2e8349db5_D_8c19a4ac_D_6547d9ea")&&RNameRules.OldName("Mr.T_CO2_D22_RAW_D_da0e1468_D_dc978fd4")&&RNameRules.OldName("Mr.T_VAC_D28_RAW_SP_1"),"Recognizes R hash chains, density hash chains, and density variants");
        Check(RNameRules.OldName("Mr.T_CO2_D22_RAW_E_1234abcd")&&RNameRules.OldName("Mr.T_Net_7ad24ed2e8349db5"),"Recognizes legacy edited and net hash names");
        Check(!RNameRules.OldName("Mr.T_OXY_D12_RAW")&&!RNameRules.OldName("foreign_D_1234abcd"),"Does not classify clean family names or foreign hashes as Mr.T variants");
    }
}
