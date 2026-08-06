using System.Collections.Generic;

namespace CTSHIPDashboard.Services
{
    public static class EncounterLookups
    {
        public static readonly List<string> TypesOfVisit = new()
        {
            "New visit",
            "Follow-up visit",
            "Emergency",
            "Preventive visit",
            "Referral visit"
        };

        public static readonly List<string> PresentingComplaints = new()
        {
            "Fever",
            "Cough",
            "Difficulty breathing",
            "Diarrhoea",
            "Vomiting",
            "Headache",
            "Abdominal pain",
            "Hypertension review",
            "Diabetes review",
            "Antenatal Care (ANC)",
            "Postnatal Care (PNC)",
            "Family Planning",
            "Immunization",
            "Child Welfare",
            "Nutrition",
            "Injury",
            "Skin disease",
            "Eye problem",
            "Mental health",
            "Other"
        };

        public static readonly List<string> Diagnoses = new()
        {
            "Uncomplicated malaria",
            "Severe malaria",
            "Acute respiratory infection",
            "Pneumonia",
            "Diarrhoeal disease",
            "Hypertension",
            "Diabetes mellitus",
            "Urinary tract infection",
            "Typhoid fever",
            "Anaemia",
            "Eye problem",
            "Skin infection",
            "Peptic ulcer disease",
            "Pregnancy-related conditions",
            "Postnatal conditions",
            "Malnutrition",
            "Musculoskeletal disorders",
            "Mental health disorders",
            "Trauma/Injury",
            "Other"
        };

        public static readonly Dictionary<string, List<string>> ServicesProvided = new()
        {
            ["General"] = new()
            {
                "General Outpatient Consultation",
                "Counselling",
                "Referral Services",
                "Emergency First Aid and Stabilization",
                "Basic Dental Care",
                "Eye Care Services",
                "Preventive Services"
            },
            ["Laboratory"] = new()
            {
                "Malaria RDT",
                "Malaria Microscopy",
                "Haemoglobin (Hb) / PCV",
                "Blood Group & Rh",
                "Blood Glucose",
                "Urinalysis",
                "Urine Pregnancy Test",
                "HIV Rapid Test",
                "Syphilis Rapid Test",
                "HBsAg",
                "Hepatitis C Antibody",
                "Stool Microscopy",
                "Stool Occult Blood",
                "Sickle Cell Screening",
                "Full Blood Count",
                "ESR",
                "Widal Test"
            },
            ["Pharmacy"] = new()
            {
                "Analgesics/Antipyretics",
                "Antibiotics",
                "Antimalarials",
                "Anthelmintics",
                "ORS & Zinc",
                "Vitamins & Supplements",
                "Maternal Health Medicines",
                "Family Planning Commodities",
                "Antihypertensive Medicines",
                "Antidiabetic Medicines",
                "Respiratory Medicines",
                "Gastrointestinal Medicines",
                "Antihistamines",
                "Dermatological Preparations",
                "Eye & Ear Preparations",
                "Intravenous Fluids",
                "Emergency Medicines",
                "Other Common Medicines"
            },
            ["Maternity"] = new()
            {
                "ANC",
                "Delivery",
                "Postnatal Care"
            },
            ["Surgery"] = new()
            {
                "Minor Surgical Procedures and Wound Care"
            }
        };

        public static readonly List<string> PatientOutcomes = new()
        {
            "Treated",
            "Discharged",
            "Follow-up appointment",
            "Referred",
            "Admitted",
            "Absconded",
            "Death"
        };

        public static readonly List<string> SexOptions = new()
        {
            "Male",
            "Female",
            "Other"
        };
    }
}