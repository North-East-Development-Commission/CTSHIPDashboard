namespace CTSHIPDashboard.Models
{
    public static class EncounterServiceCatalog
    {
        public const string Outpatient = "Outpatient";
        public const string Inpatient = "Inpatient";

        public const string ConsultationService = "Consultation";
        public const string MchService = "Maternal and child health (MCH)";
        public const string NcdService = "Non-communicable disease (NCD) services";
        public const string LaboratoryService = "Laboratory services";
        public const string PrescriptionService = "Drug prescription/dispensing";

        public static readonly IReadOnlyList<string> OutpatientServices = new[]
        {
            ConsultationService,
            MchService,
            NcdService,
            LaboratoryService,
            PrescriptionService,
            "Antenatal care (ANC)",
            "Postnatal care (PNC)",
            "Family planning services",
            "Integrated Management of Childhood Illnesses (IMCI)",
            "Growth monitoring",
            "Nutrition assessment",
            "Vitamin A supplementation",
            "Deworming",
            "Malaria testing and treatment",
            "Tuberculosis screening and referral",
            "HIV counselling and testing",
            "STI screening and treatment",
            "Management of common infectious diseases",
            "Hypertension screening and management",
            "Diabetes screening and management",
            "Health education on NCD prevention",
            "Routine immunization",
            "Health promotion and education",
            "Nutrition counselling",
            "Environmental health education",
            "Malaria Rapid Diagnostic Test (RDT)",
            "Urinalysis",
            "Pregnancy test",
            "Blood glucose testing",
            "Hemoglobin estimation",
            "HIV rapid testing",
            "Wound dressing",
            "Incision and drainage of simple abscesses",
            "Simple suturing",
            "Ear syringing",
            "Nebulization (where available)"
        };

        public static readonly IReadOnlyList<string> InpatientServices = new[]
        {
            LaboratoryService,
            PrescriptionService,
            "Normal delivery",
            "Monitoring during labour",
            "Immediate newborn care",
            "Management of uncomplicated postpartum conditions",
            "Observation following delivery",
            "Observation for uncomplicated childhood illnesses",
            "Management of mild dehydration",
            "Monitoring of children requiring short-term care",
            "Observation for uncomplicated malaria",
            "Observation for mild illnesses requiring monitoring",
            "Fluid therapy for uncomplicated cases",
            "Monitoring before referral",
            "First aid and emergency care",
            "Stabilization of emergencies before referral",
            "Management of uncomplicated emergencies",
            "Referral of severe cases to secondary facilities"
        };

        public static readonly IReadOnlySet<string> ImmunizationServices =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Routine immunization",
                "Vitamin A supplementation",
                "Deworming"
            };

        public static readonly IReadOnlySet<string> AncServices =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Antenatal care (ANC)"
            };

        public static readonly IReadOnlySet<string> FamilyPlanningServices =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Family planning services"
            };

        public static readonly IReadOnlySet<string> HealthPromotionServices =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Health promotion and education",
                "Health education on NCD prevention",
                "Nutrition counselling",
                "Environmental health education"
            };

        public static readonly IReadOnlySet<string> OtherPreventiveServices =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                MchService,
                "Postnatal care (PNC)",
                "Integrated Management of Childhood Illnesses (IMCI)",
                "Growth monitoring",
                "Nutrition assessment",
                "Tuberculosis screening and referral",
                "HIV counselling and testing",
                "STI screening and treatment",
                "Hypertension screening and management",
                "Diabetes screening and management",
                "Malaria Rapid Diagnostic Test (RDT)",
                "Pregnancy test",
                "Blood glucose testing",
                "Hemoglobin estimation",
                "HIV rapid testing"
            };

        public static bool IsValid(string setting, string service)
        {
            IReadOnlyList<string> services = setting == Inpatient ? InpatientServices : OutpatientServices;
            return services.Contains(service, StringComparer.OrdinalIgnoreCase);
        }
    }
}
