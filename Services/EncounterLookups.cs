using System.Collections.Generic;

namespace CTSHIPDashboard.Services
{
    public static class EncounterLookups
    {
        public static readonly List<string> TypesOfVisit = new()
        {
            "New visit", "Follow-up visit", "Emergency"
        };

        public static readonly List<string> PresentingComplaints = new()
        {
            "Fever", "Cough", "Difficulty breathing", "Diarrhea", "Vomiting", "Headache", "Abdominal pain",
            "Hypertension review", "Diabetes review", "Antenatal Care (ANC)", "Postnatal Care (PNC)",
            "Family Planning", "Immunization", "Child Welfare", "Nutrition", "Injury", "Skin disease",
            "Eye problem", "Mental health", "Other"
        };

        public static readonly List<string> Diagnoses = new()
        {
            "Uncomplicated malaria", "Severe malaria", "Acute respiratory infection", "Pneumonia",
            "Diarrhea disease", "Hypertension", "Diabetes mellitus", "Urinary tract infection", "Typhoid fever",
            "Anemia", "Eye problem", "Skin infection", "Peptic ulcer disease", "Pregnancy-related conditions",
            "Postnatal conditions", "Malnutrition", "Musculoskeletal disorders", "Mental health disorders",
            "Trauma/Injury", "Other"
        };

        public static readonly List<string> ServicesProvided = new()
        {
            "General Outpatient Consultation", "Laboratory Services", "Counseling", "Malaria Diagnosis and Treatment",
            "Mental Health Screening and Basic Care", "Adolescent Health Services", "Emergency First Aid and Stabilization",
            "Referral Services", "Basic Dental Care", "Preventive Services", "Delivery",
            "Minor Surgical Procedures and Wound Care", "Pharmacy and Medicines Dispensing Service", "Others"
        };

        public static readonly List<string> LaboratoryTests = new()
        {
            "Malaria Rapid Diagnostic Test (RDT)", "Malaria Microscopy (where available)",
            "Haemoglobin (Hb) / Packed Cell Volume (PCV)", "Blood Group and Rhesus (Rh) Factor",
            "Blood Glucose (Random/Fasting)", "Urinalysis (Protein, Glucose, Ketones, Blood, etc.)",
            "Urine Pregnancy Test (UPT)", "HIV Rapid Test", "Syphilis Rapid Test",
            "Hepatitis B Surface Antigen (HBsAg) Test", "Hepatitis C Antibody Test",
            "Stool Microscopy for Ova and Parasites", "Stool Occult Blood Test", "Sickle Cell Screening",
            "Full Blood Count (FBC)", "Erythrocyte Sedimentation Rate (ESR)", "Widal Test", "Others"
        };

        public static readonly Dictionary<string, List<string>> Medicines = new()
        {
            ["Analgesics and Antipyretics"] = new() { "Paracetamol", "Ibuprofen", "Diclofenac" },
            ["Antibiotics"] = new() { "Amoxicillin", "Amoxicillin-Clavulanate", "Cotrimoxazole", "Metronidazole", "Azithromycin", "Erythromycin", "Ciprofloxacin", "Cefuroxime" },
            ["Antimalarials"] = new() { "Artemether-Lumefantrine (ACT)", "Artesunate-Amodiaquine", "Injectable Artesunate (for pre-referral treatment)" },
            ["Anthelmintics"] = new() { "Albendazole", "Mebendazole" },
            ["Oral Rehydration and Zinc"] = new() { "Oral Rehydration Salts (ORS)", "Zinc Sulphate tablets/syrup" },
            ["Vitamins and Nutritional Supplements"] = new() { "Vitamin A", "Folic Acid", "Iron/Folic Acid tablets", "Multivitamins" },
            ["Maternal Health Medicines"] = new() { "Ferrous Sulphate", "Calcium tablets", "Oxytocin", "Misoprostol", "Magnesium Sulphate" },
            ["Family Planning Commodities"] = new() { "Combined oral contraceptive pills", "Progestin-only pills", "Injectable contraceptives", "Implants", "Emergency contraceptive pills", "Condoms" },
            ["Antihypertensive Medicines"] = new() { "Amlodipine", "Nifedipine", "Hydrochlorothiazide", "Methyldopa" },
            ["Antidiabetic Medicines"] = new() { "Metformin", "Glibenclamide", "Insulin" },
            ["Respiratory Medicines"] = new() { "Salbutamol tablets/inhaler", "Salbutamol syrup", "Aminophylline" },
            ["Gastrointestinal Medicines"] = new() { "Omeprazole", "Antacids", "Hyoscine Butylbromide", "Loperamide" },
            ["Antihistamines"] = new() { "Chlorpheniramine", "Cetirizine", "Loratadine" },
            ["Dermatological Preparations"] = new() { "Clotrimazole cream", "Hydrocortisone cream", "Benzyl Benzoate lotion", "Gentian Violet", "Povidone-Iodine" },
            ["Eye and Ear Preparations"] = new() { "Chloramphenicol eye drops", "Tetracycline eye ointment", "Ciprofloxacin eye/ear drops" },
            ["Intravenous Fluids"] = new() { "Normal Saline", "Ringer's Lactate", "5% Dextrose" },
            ["Emergency Medicines"] = new() { "Adrenaline (Epinephrine)", "Diazepam", "Hydrocortisone injection", "Atropine", "Injectable Artesunate" },
            ["Other Common Medicines"] = new() { "Aspirin (selected indications)", "Oral antifungal agents (e.g., Fluconazole)", "Topical antiseptics", "Other Medicines" }
        };

        public static readonly List<string> PreventiveServices = new()
        {
            "Immunization", "Health education", "Family Planning", "Nutrition Assessment and Counselling",
            "Growth Monitoring and Promotion", "Screening Test", "Antenatal Care (ANC)", "Postnatal Care (PNC)", "Others"
        };

        public static readonly List<string> Immunizations = new()
        {
            "BCG Vaccine", "Hepatitis B Vaccine", "Oral Polio Vaccine (OPV)", "Inactivated Polio Vaccine (IPV)",
            "Pentavalent Vaccine (DPT-HepB-Hib)", "Pneumococcal Conjugate Vaccine (PCV)", "Rotavirus Vaccine",
            "Measles-Rubella (MR) Vaccine", "Yellow Fever Vaccine", "Meningococcal A (MenA) Vaccine"
        };

        public static readonly List<string> ScreeningTests = new()
        {
            "Blood pressure screening", "Blood glucose screening", "HIV rapid testing", "Tuberculosis symptom screening",
            "Malaria rapid diagnostic testing", "Pregnancy testing", "Anaemia screening", "Urinalysis",
            "Nutritional assessment (BMI/MUAC)", "Growth monitoring for children", "Cervical cancer screening (where capacity exists)",
            "Clinical breast examination", "Vision screening", "Mental health screening", "Routine immunization status assessment"
        };

        public static readonly List<string> PatientOutcomes = new()
        {
            "Treated", "Discharged", "Follow-up appointment", "Referred", "Admitted", "Absconded", "Death"
        };

        public static readonly List<string> SexOptions = new() { "Male", "Female", "Other" };
    }
}


