// Data/SeedData.cs
using Bogus;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Ensure DB exists
            context.Database.EnsureCreated();

            // ROLES
            string[] roles = { "Admin", "CTSHIPAdmin", "HMO", "HmoEnrollmentOfficer", "Provider", "SSHIA", "IHSA", "Monitoring" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // ADMIN USER
            var adminEmail = "admin@nedc.gov.ng";
            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "CTSHIP NEDC Administrator",
                    ContactInfo = "0809-000-0001",
                    State = "Borno"
                };
                await userManager.CreateAsync(admin, "Admin@2025");
            }

            if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }

            if (!await userManager.IsInRoleAsync(admin, "CTSHIPAdmin"))
            {
                await userManager.AddToRoleAsync(admin, "CTSHIPAdmin");
            }


            // Run only once
            if (context.Enrollees.Any()) return;

            // Ensure Hmos exist first
            SeedHmos(context);
            //SeedClaims(context);
            context.SaveChanges();

            var hmos = context.Hmos.ToList();
            var provider = context.Providers.ToList();
            var states = GetNigerianStates();
            var faker = new Faker("en_NG"); // Nigerian locale

            var enrollees = new List<Enrollee>();

            for (int i = 1; i <= 100; i++)
            {
                var gender = faker.Person.Gender == Bogus.DataSets.Name.Gender.Male ? "Male" : "Female";
                var firstName = faker.Person.FirstName;//(gender == "Male" ? Bogus.DataSets.Name.Gender.Male : Bogus.DataSets.Name.Gender.Female);
                var lastName = faker.Person.LastName;
                var state = faker.PickRandom(states);
                var hmo = faker.PickRandom(hmos);
                var providerS = faker.PickRandom(provider);


                var enrollee = new Enrollee
                {
                    FullName = $"{firstName} {lastName}",
                    Gender = gender,
                    DateOfBirth = faker.Date.Past(50, DateTime.Now.AddYears(-18)),
                    Phone = faker.Phone.PhoneNumber("080########"),
                    //Email = faker.Internet.Email(firstName, lastName).ToLower(),
                    Address = faker.Address.StreetAddress() + ", " + faker.Address.City(),
                    State = state,
                    NIN = 43546545453,
                    LGA = faker.PickRandom(GetLGAsForState(state)),
                    HmoId = hmo.Id,
                    ProviderId = providerS.Id,
                    DateRegistered = faker.Date.Between(DateTime.Now.AddYears(-3), DateTime.Now),
                    Status = faker.Random.Bool(0.95f) ? "Active" : "Inactive", // 95% active
                    RegisteredBy = "System Seed"
                };

                // Generate realistic Enrollment Number
                var year = enrollee.DateRegistered.Year;
                var stateCode = GetStateCode(state);
                enrollee.EnrollmentNumber = $"CTH-{year}-{stateCode}-{i + 1000:D6}";

                enrollees.Add(enrollee);
            }

            context.Enrollees.AddRange(enrollees);
            context.SaveChanges();

            Console.WriteLine("100 REALISTIC NIGERIAN ENROLLEES SEEDED SUCCESSFULLY!");
        }

        public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
        {
            List<ReferredHospital> hospitals = new List<ReferredHospital>
        {
            new ReferredHospital
            {
                Name = "University of Maiduguri Teaching Hospital",
                State = "Borno",
                Lga = "Maiduguri Metropolitan",
                Address = "Bama Road, Maiduguri, Borno State",
                ContactPerson = "Referral Desk",
                PhoneNumber = "08000000001",
                IsActive = true
            },
            new ReferredHospital
            {
                Name = "Federal Medical Centre Yola",
                State = "Adamawa",
                Lga = "Yola North",
                Address = "Yola, Adamawa State",
                ContactPerson = "Referral Desk",
                PhoneNumber = "08000000002",
                IsActive = true
            },
            new ReferredHospital
            {
                Name = "Federal Teaching Hospital Gombe",
                State = "Gombe",
                Lga = "Gombe",
                Address = "Ashaka Road, Gombe State",
                ContactPerson = "Referral Desk",
                PhoneNumber = "08000000003",
                IsActive = true
            },
            new ReferredHospital
            {
                Name = "Abubakar Tafawa Balewa University Teaching Hospital",
                State = "Bauchi",
                Lga = "Bauchi",
                Address = "Bauchi, Bauchi State",
                ContactPerson = "Referral Desk",
                PhoneNumber = "08000000004",
                IsActive = true
            }
        };

            foreach (ReferredHospital hospital in hospitals)
            {
                bool exists = await context.ReferralHospitals
                    .AnyAsync(x => x.Name == hospital.Name && x.State == hospital.State, cancellationToken);

                if (!exists)
                {
                    context.ReferralHospitals.Add(hospital);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        public static void SeedEnrollee(ApplicationDbContext context)
        {
            // Prevent duplicate seeding
            if (context.Enrollees.Any()) return;

            var random = new Random();
            var hmos = context.Hmos.ToList();
            var providers = context.Providers.ToList();
            var states = GetNigerianStates();

            // Common Nigerian first & last names (realistic mix)
            var maleFirstNames = new[] { "Chukwudi", "Ibrahim", "Adebayo", "Emeka", "Yusuf", "Olumide", "Musa", "Tunde", "Abdullahi", "Kehinde", "Segun", "Usman", "Tope", "Sani", "Babatunde" };
            var femaleFirstNames = new[] { "Fatima", "Aisha", "Chioma", "Ngozi", "Zainab", "Khadijat", "Blessing", "Grace", "Aminat", "Patience", "Funmilayo", "Rukayat", "Chidera", "Halima", "Omolara" };
            var lastNames = new[] { "Mohammed", "Adeyemi", "Okafor", "Bello", "Yusuf", "Eze", "Abdullahi", "Okonkwo", "Ibrahim", "Balogun", "Nwachukwu", "Suleiman", "Aliyu", "Hassan", "Salami", "Lawal" };

            var enrollees = new List<Enrollee>();

            for (int i = 1; i <= 100; i++)
            {
                bool isMale = random.Next(0, 2) == 0;
                string firstName = isMale
                    ? maleFirstNames[random.Next(maleFirstNames.Length)]
                    : femaleFirstNames[random.Next(femaleFirstNames.Length)];
                string lastName = lastNames[random.Next(lastNames.Length)];

                var state = states[random.Next(states.Length)];
                var hmo = hmos[random.Next(hmos.Count)];
                var provider = providers[random.Next(providers.Count)];

                var birthYear = random.Next(1960, 2006);
                var birthMonth = random.Next(1, 13);
                var birthDay = random.Next(1, DateTime.DaysInMonth(birthYear, birthMonth));


                var enrollee = new Enrollee
                {
                    FullName = $"{firstName} {lastName}",
                    Gender = isMale ? "Male" : "Female",
                    DateOfBirth = new DateTime(birthYear, birthMonth, birthDay),
                    Phone = $"08{random.Next(0, 2)}{random.Next(0, 10)}{random.Next(1000000, 9999999)}",
                    //Email = $"{firstName.ToLower()}.{lastName.ToLower()}@example.com",
                    Address = $"{random.Next(1, 200)} {GetRandomStreet()} Street, {GetRandomCity(state)}",
                    State = state,
                    LGA = GetRandomLGA(state),
                    Ward = GetRandomWard(state),
                    HmoId = hmo.Id,
                    ProviderId = provider.Id,
                    Status = random.Next(0, 100) < 94 ? "Active" : "Inactive", // 94% active
                    DateRegistered = DateTime.Now.AddDays(-random.Next(30, 1095)), // 1 month to 3 years ago
                    RegisteredBy = "System Seed",
                    EnrollmentNumber = GenerateEnrollmentNumber(state, i, random)
                };

                enrollees.Add(enrollee);
            }

            context.Enrollees.AddRange(enrollees);
            context.SaveChanges();

            Console.WriteLine($"ENROLLEES SEED COMPLETE: {enrollees.Count} REAL NIGERIANS REGISTERED!");
            Console.WriteLine("   Names: Fatima Yusuf, Chukwudi Eze, Aisha Mohammed, Adebayo Balogun...");
            Console.WriteLine("   States: All 36 + FCT | Phones: 0803M, Airtel, Glo | Emails: Realistic");
        }


        public static void SeedHmos(ApplicationDbContext context)
        {
            if (context.Hmos.Any()) return;

            var hmos = new[]
            {
                new Hmo { Name = "Hygeia HMO Limited", Email = "info@hygeiahmo.com", Phone = "01-4618888", Address = "8 Louis Solomon Close, Victoria Island, Borno", State = "Borno", RegistrationNumber = "HMO-20200101000881", Status = "Active" },
                new Hmo { Name = "Clearline HMO", Email = "support@clearlinehmo.com", Phone = "01-3429999", Address = "26 Commercial Avenue, Sabo Yaba, Yobe", State = "Yobe",  RegistrationNumber = "HMO-20200107890001",  Status = "Active" },
                new Hmo { Name = "Multishield Limited", Email = "info@multishield.com.ng", Phone = "09-4615000", Address = "Plot 1005, Ahmadu Bello Way, Gombe", State = "Gombe",  RegistrationNumber = "HMO-20200181009901",  Status = "Non-Active" },
                new Hmo { Name = "United Healthcare International", Email = "care@unitedhmo.com", Phone = "01-2773400", Address = "2 Ajose Adeogun Street, VI, Adamawa", State = "Adamawa",  RegistrationNumber = "HMO-20270101000001", Status = "Active" },
                new Hmo { Name = "Premium Health Limited", Email = "info@premiumhealth.com.ng", Phone = "0809-999-0001", Address = "Kano Office Complex, Taraba", State = "Taraba",  RegistrationNumber = "HMO-20200101060001", Status = "Active" },
                new Hmo { Name = "Total Health Trust", Email = "enquiries@totalhealthtrust.com", Phone = "01-4486666", Address = "7A Milverton Road, Ikoyi, Bauchi", State = "Bauchi",  RegistrationNumber = "HMO-20209901000001", Status = "Active" },
            };

            context.Hmos.AddRange(hmos);
        }

        public static void SeedEncounters(ApplicationDbContext context)
        {
            if (context.Encounters.Any()) return;

            var random = new Random();
            var enrollees = context.Enrollees.ToList();
            var providers = context.Providers.ToList();
            var doctors = context.Doctors.ToList();

            if (!enrollees.Any() || !providers.Any())
            {
                Console.WriteLine("Warning: Enrollees or Providers not found. Skipping encounter seeding.");
                return;
            }

            var visitTypes = new[] { "Outpatient", "Emergency", "ANC", "Immunization", "Laboratory", "Surgery", "Follow-up" };
            var complaints = new[] { "Fever", "Abdominal pain", "Cough", "Routine check", "Malaria test", "Delivery", "Hypertension" };
            var diagnoses = new[] { "Malaria", "Typhoid", "Hypertension", "Pregnancy", "URI", "Normal Delivery", "Appendicitis" };

            var encounters = new List<Encounter>();

            foreach (var enrollee in enrollees.Take(600))
            {
                int visitCount = random.Next(1, 10); // 1–9 visits per enrollee

                for (int i = 0; i < visitCount; i++)
                {
                    var provider = providers[random.Next(providers.Count)];
                    var providerDoctors = doctors.Where(doctor => doctor.ProviderId == provider.Id).ToList();
                    var doctor = providerDoctors.Count > 0
                        ? providerDoctors[random.Next(providerDoctors.Count)]
                        : null;

                    var visitDate = enrollee.DateRegistered
                        .AddDays(random.Next(5, 800))
                        .AddHours(random.Next(8, 19))
                        .AddMinutes(random.Next(0, 60));

                    var isEmergency = random.Next(0, 10) == 0;
                    var hasLab = random.Next(0, 3) > 0;
                    var hasDrugs = random.Next(0, 2) == 0;

                    var encounter = new Encounter
                    {
                        EnrolleeId = enrollee.Id,
                        ProviderId = provider.Id,
                        DoctorId = doctor?.Id,
                        VisitDate = visitDate,

                        VisitType = isEmergency ? "Emergency" : visitTypes[random.Next(visitTypes.Length)],
                        ChiefComplaint = complaints[random.Next(complaints.Length)],
                        Diagnosis = diagnoses[random.Next(diagnoses.Length)],

                        // Vital Signs
                        Temperature = Math.Round(36.5m + (decimal)random.NextDouble() * 2.5m, 1), // 36.5–39.0
                        BloodPressure = random.Next(0, 10) < 8
                            ? $"{random.Next(100, 160)}/{random.Next(60, 100)}"
                            : "120/80",
                        PulseRate = random.Next(60, 110),

                        // Fees
                        ConsultationFee = isEmergency ? 5000.00m : 2000.00m,
                        LabFee = hasLab ? random.Next(5000, 35000) : 0m,
                        DrugFee = hasDrugs ? random.Next(8000, 65000) : 0m,

                        SeenBy = doctor?.FullName,
                        Rank = doctor?.Designation ?? doctor?.Specialty,

                        Notes = random.Next(0, 4) == 0
                            ? "Patient responded well to treatment. Review in 2 weeks."
                            : null,

                        LabTests = hasLab
                            ? "FBC, Malaria Parasite, Widal Test, Urinalysis"
                            : null,

                        TreatmentGiven = hasDrugs
                            ? "Artesunate Injection, Paracetamol, ORS, Amoxicillin"
                            : "Counseling and advice given",

                        Status = random.Next(0, 10) < 7 ? "Completed" : "Billed",
                        IsBilled = random.Next(0, 10) < 8,

                        EncounterNumber = $"ENC-{visitDate:yyyyMMdd}-{random.Next(1000, 9999)}"
                    };

                    encounters.Add(encounter);
                }
            }

            context.Encounters.AddRange(encounters);
            context.SaveChanges();

            Console.WriteLine($"SUCCESS: {encounters.Count} ENCOUNTERS SEEDED!");
            Console.WriteLine("   Doctors: Dr. Adebayo, Dr. Fatima Yusuf, Dr. Chukwuemeka Eze, etc.");
            Console.WriteLine("   Total Amount = Consultation + Lab + Drugs calculated automatically");
        }
        // ADD TO YOUR EXISTING SeedData CLASS
        public static void SeedProviders(ApplicationDbContext context)
        {
            if (context.Providers.Any(p => p.State == "Adamawa" ||
                                           p.State == "Bauchi" ||
                                           p.State == "Borno" ||
                                           p.State == "Gombe" ||
                                           p.State == "Taraba" ||
                                           p.State == "Yobe")) return;

            var northEastProviders = new[]
            {
        // BORNO STATE
        new Provider { Code = "UMTH021", Name = "University of Maiduguri Teaching Hospital (UMTH)", Phone="09065756565", State = "Borno", Level = "Tertiary", IsActive = true, DateRegistered = new DateTime(2020, 1, 20) },
        new Provider { Code = "SMH022", Name = "State Specialist Hospital Maiduguri",  Phone="09065756565", State = "Borno", Level = "Secondary", IsActive = true, DateRegistered = new DateTime(2021, 3, 15) },
        new Provider { Code = "NGH023", Name = "General Hospital Biu",  Phone="09065756565", State = "Borno", Level = "Secondary", IsActive = true, DateRegistered = new DateTime(2021, 6, 10) },
        new Provider { Code = "MCH024", Name = "Mafa Cottage Hospital",  Phone="09065756565", State = "Borno", Level = "Primary", IsActive = true, DateRegistered = new DateTime(2022, 2, 5) },

        // ADAMAWA STATE
        new Provider { Code = "FMCY025", Name = "Federal Medical Centre Yola", Phone="09065756565", State = "Adamawa", Level = "Tertiary", IsActive = true, DateRegistered = new DateTime(2020, 4, 12) },
        new Provider { Code = "SSH026", Name = "Specialist Hospital Yola", Phone="09065756565", State = "Adamawa", Level = "Secondary", IsActive = true, DateRegistered = new DateTime(2021, 5, 18) },
        new Provider { Code = "GHC027", Name = "General Hospital Ganye", Phone="09065756565", State = "Adamawa", Level = "Secondary", IsActive = true, DateRegistered = new DateTime(2021, 8, 22) },
        new Provider { Code = "MCH028", Name = "Michika General Hospital", Phone="09065756565", State = "Adamawa", Level = "Secondary", IsActive = true, DateRegistered = new DateTime(2022, 1, 14) },

        // BAUCHI STATE
        new Provider { Code = "ATBUTH029", Name = "Abubakar Tafawa Balewa University Teaching Hospital", Phone="09065756565", State = "Bauchi", Level = "Tertiary", IsActive = true, DateRegistered = new DateTime(2020, 7, 8) },
        new Provider { Code = "SSH030", Name = "Specialist Hospital Bauchi", Phone="09065756565", State = "Bauchi", Level = "Secondary", IsActive = true, DateRegistered = new DateTime(2021, 9, 20) },
        new Provider { Code = "GHD031", Name = "General Hospital Dass",  Phone="09065756565", State = "Bauchi", Level = "Secondary", IsActive = true, DateRegistered = new DateTime(2022, 3, 10) },

        // GOMBE STATE
        new Provider { Code = "FMCG032", Name = "Federal Medical Centre Gombe",  Phone="09065756565", State = "Gombe", Level = "Tertiary", IsActive = true, DateRegistered = new DateTime(2020, 9, 15) },
        new Provider { Code = "SGH033", Name = "State Specialist Hospital Gombe", Phone="09065756565", State = "Gombe", Level = "Secondary", IsActive = true, DateRegistered = new DateTime(2021, 10, 25) },
        new Provider { Code = "PHC034", Name = "Kaltungo General Hospital", Phone="09065756565", State = "Gombe", Level = "Secondary", IsActive = true, DateRegistered = new DateTime(2022, 4, 18) },

        // TARABA STATE
        new Provider { Code = "FMCJ035", Name = "Federal Medical Centre Jalingo",  Phone="09065756565", State = "Taraba", Level = "Tertiary", IsActive = true, DateRegistered = new DateTime(2020, 11, 10) },
        new Provider { Code = "SGH036", Name = "Specialist Hospital Jalingo", Phone="09065756565", State = "Taraba", Level = "Secondary", IsActive = true, DateRegistered = new DateTime(2021, 12, 5) },
        new Provider { Code = "GHC037", Name = "General Hospital Wukari", Phone="09065756565", State = "Taraba", Level = "Secondary", IsActive = true, DateRegistered = new DateTime(2022, 5, 12) },

        // YOBE STATE
        new Provider { Code = "YTH038", Name = "Yobe State University Teaching Hospital Damaturu", Phone="09065756565", State = "Yobe", Level = "Tertiary", IsActive = true, DateRegistered = new DateTime(2021, 2, 18) },
        new Provider { Code = "SGH039", Name = "General Hospital Damaturu",  Phone="09065756565",State = "Yobe", Level = "Secondary", IsActive = true, DateRegistered = new DateTime(2021, 11, 30) },
        new Provider { Code = "PHC040", Name = "Potiskum General Hospital", Phone="09065756565", State = "Yobe", Level = "Secondary", IsActive = true, DateRegistered = new DateTime(2022, 6, 20) }
    };

            context.Providers.AddRange(northEastProviders);
            context.SaveChanges();

            Console.WriteLine("SUCCESS: 20 NORTH-EAST NIGERIAN HOSPITALS SEEDED!");
            Console.WriteLine("   States: Borno • Adamawa • Bauchi • Gombe • Taraba • Yobe");
            Console.WriteLine("   Includes: UMTH, FMC Yola, FMC Gombe, ATBUTH, Yobe State Teaching Hospital");
        }

        public static void SeedDoctors(ApplicationDbContext context)
        {
            if (context.Doctors.Any()) return;

            var providers = context.Providers.OrderBy(provider => provider.Id).ToList();
            if (providers.Count == 0) return;

            string[] names =
            {
                "Dr. Amina Musa", "Dr. Ibrahim Sani", "Dr. Fatima Yusuf",
                "Dr. Chukwuemeka Eze", "Dr. Aisha Mohammed", "Dr. Ngozi Okonkwo",
                "Dr. Musa Aliyu", "Dr. Zainab Abdullahi", "Dr. Kemi Ogunleye",
                "Dr. Emeka Nwosu", "Dr. Halima Bello", "Dr. Tunde Balogun"
            };
            string[] specialties =
            {
                "General Practice", "Family Medicine", "Internal Medicine",
                "Paediatrics", "Obstetrics and Gynaecology", "Emergency Medicine"
            };

            var doctors = new List<Doctor>();
            int doctorIndex = 0;

            foreach (Provider provider in providers)
            {
                for (int facilityDoctor = 1; facilityDoctor <= 2; facilityDoctor++)
                {
                    string name = names[doctorIndex % names.Length];
                    string specialty = specialties[doctorIndex % specialties.Length];
                    doctors.Add(new Doctor
                    {
                        ProviderId = provider.Id,
                        FullName = name,
                        MedicalLicenseNumber = $"MDCN-{provider.Id:D4}-{facilityDoctor:D2}",
                        Specialty = specialty,
                        Designation = facilityDoctor == 1 ? "Medical Officer" : "Consultant",
                        IsActive = true,
                        DateAdded = DateTime.UtcNow
                    });
                    doctorIndex++;
                }
            }

            context.Doctors.AddRange(doctors);
            context.SaveChanges();
            Console.WriteLine($"DOCTOR SEED COMPLETE: {doctors.Count} PROVIDER-OWNED DOCTORS REGISTERED!");
        }

        public static async Task SeedAdminUser(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Ensure roles exist (including NHIA and StateOffice)
            string[] roles = { "Admin", "CTSHIPAdmin", "HMO", "HmoEnrollmentOfficer", "Provider", "Auditor", "Finance", "Reviewer", "StateOffice", "NHIA", "SSHIA", "IHSA", "Monitoring" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                    Console.WriteLine($"Role created: {role}");
                }
            }

            var adminEmail = "as.maiwada@nedc.gov.ng";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "CTSHIP NEDC Administrator",
                    ContactInfo = "0809-000-0001",
                    State = "Borno",
                    EmailConfirmed = true,
                    LockoutEnabled = false
                };

                var createAdmin = await userManager.CreateAsync(adminUser, "Admin@2025");
                if (!createAdmin.Succeeded)
                {
                    Console.WriteLine("Failed to create admin user: " + string.Join(", ", createAdmin.Errors.Select(e => e.Description)));
                    adminUser = null;
                }
            }

            if (adminUser != null)
            {
                if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }

                if (!await userManager.IsInRoleAsync(adminUser, "CTSHIPAdmin"))
                {
                    await userManager.AddToRoleAsync(adminUser, "CTSHIPAdmin");
                }
            }

            // NHIA national user
            var nhiaEmail = "nhia@nedc.gov.ng";
            var nhiaUser = await userManager.FindByEmailAsync(nhiaEmail);
            if (nhiaUser == null)
            {
                nhiaUser = new ApplicationUser
                {
                    UserName = nhiaEmail,
                    Email = nhiaEmail,
                    FullName = "NHIA National Officer",
                    ContactInfo = "0800-NHIA",
                    State = "",
                    EmailConfirmed = true,
                    LockoutEnabled = false
                };

                var createNhia = await userManager.CreateAsync(nhiaUser, "Nhia@2025!");
                if (createNhia.Succeeded)
                {
                    await userManager.AddToRoleAsync(nhiaUser, "NHIA");
                    Console.WriteLine("NHIA user created: nhia@nedc.gov.ng");
                }
                else
                {
                    Console.WriteLine("Failed to create NHIA user: " + string.Join(", ", createNhia.Errors.Select(e => e.Description)));
                }
            }

            // Create one StateOffice user per seeded state
            var states = GetNigerianStates();
            foreach (var state in states)
            {
                if (string.IsNullOrWhiteSpace(state)) continue;
                var email = $"stateofficer.{state.ToLowerInvariant()}@nedc.gov.ng";
                var existing = await userManager.FindByEmailAsync(email);
                if (existing != null) continue;

                var stateUser = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = $"{state} State Officer",
                    ContactInfo = "0800-STATEOFF",
                    State = state,
                    EmailConfirmed = true,
                    LockoutEnabled = false
                };

                var created = await userManager.CreateAsync(stateUser, "State@2025!");
                if (created.Succeeded)
                {
                    await userManager.AddToRoleAsync(stateUser, "StateOffice");
                    Console.WriteLine($"StateOffice user created: {email} (State: {state})");
                }
                else
                {
                    Console.WriteLine($"Failed to create StateOffice for {state}: " + string.Join(", ", created.Errors.Select(e => e.Description)));
                }
            }

            // Keep other sample users (if not present)
            var sampleUsers = new[]
            {
                new { Email = "hmo@hygeia.com", Name = "Hygeia HMO Officer", Role = "HMO", Password = "Hmo@2025" },
                new { Email = "provider@luth.gov.ng", Name = "UMTH Claims Officer", Role = "Provider", Password = "Umth@2025" },
                new { Email = "auditor@nhia.gov.ng", Name = "Nedc Internal Auditor", Role = "Auditor", Password = "Audit@2025" },
                new { Email = "finance@nhia.gov.ng", Name = "Nedc Finance Director", Role = "Finance", Password = "Finance@2025" },
                new { Email = "reviewer@nhia.gov.ng", Name = "Medical Claims Reviewer", Role = "Reviewer", Password = "Review@2025" },
                new { Email = "monitoring@nedc.gov.ng", Name = "Monitoring and Evaluation Officer", Role = "Monitoring", Password = "Monitoring@2025!" }
            };

            foreach (var user in sampleUsers)
            {
                var existingUser = await userManager.FindByEmailAsync(user.Email);
                if (existingUser == null)
                {
                    var newUser = new ApplicationUser
                    {
                        UserName = user.Email,
                        Email = user.Email,
                        FullName = user.Name,
                        ContactInfo = "0800-CTSHIP-ADMIN",
                        State = states.FirstOrDefault() ?? "Borno",
                        EmailConfirmed = true
                    };

                    var createResult = await userManager.CreateAsync(newUser, user.Password);
                    if (createResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(newUser, user.Role);
                        Console.WriteLine($"User created: {user.Email} | Role: {user.Role}");
                    }
                }
                else if (!await userManager.IsInRoleAsync(existingUser, user.Role))
                {
                    await userManager.AddToRoleAsync(existingUser, user.Role);
                    Console.WriteLine($"Role assigned: {user.Email} | Role: {user.Role}");
                }
            }

            Console.WriteLine("ALL SYSTEM USERS & ROLES SEEDED SUCCESSFULLY!");
            Console.WriteLine("Login now at: /Identity/Account/Login");
        }

        // Helper
        private static string WeightedRandom(string[] items, int[] weights, Random rand)
        {
            int total = weights.Sum();
            int roll = rand.Next(total);
            int sum = 0;
            for (int i = 0; i < items.Length; i++)
            {
                sum += weights[i];
                if (roll < sum) return items[i];
            }
            return items[^1];
        }


        public static void SeedClaims(ApplicationDbContext context)
        {
            if (context.Claims.Any()) return;

            var random = new Random();
            var enrollees = context.Enrollees.ToList();
            var providers = context.Providers.ToList();

            if (!enrollees.Any() || !providers.Any())
            {
                Console.WriteLine("Enrollees or Providers not seeded. Skipping claims...");
                return;
            }

            // REAL NIGERIAN DIAGNOSES & TREATMENTS (NHIA Tariff)
            var diagnoses = new[]
            {
               "Malaria (Uncomplicated)", "Typhoid Fever", "Hypertension", "Diabetes Mellitus Type 2",
               "Peptic Ulcer Disease", "Pneumonia", "Urinary Tract Infection", "Ante-Natal Care",
               "Normal Delivery", "Caesarean Section", "Acute Appendicitis", "Inguinal Hernia",
               "Cataract Surgery", "Myomectomy (Fibroid)", "Road Traffic Accident (RTA)"
    };

            var treatments = new[]
            {
              "IV Artesunate + Paracetamol + IV Fluids", "IV Ceftriaxone + Metronidazole",
               "Amlodipine 10mg + Lifestyle Modification", "Metformin 500mg BD + Diet Control",
               "Omeprazole + Amoxicillin + Clarithromycin", "IV Antibiotics + Oxygen Therapy",
               "Ciprofloxacin + Analgesics", "Routine ANC Package", "Normal Vaginal Delivery",
               "Emergency C-Section + Blood Transfusion", "Appendicectomy", "Herniorrhaphy",
               "Phacoemulsification + IOL", "Myomectomy", "Wound Dressing + IV Antibiotics + Surgery"
            };

            var baseAmounts = new[]
            {
                42000m, 68000m, 35000m, 52000m, 78000m, 125000m, 48000m, 85000m,
                285000m, 650000m, 195000m, 485000m, 720000m, 980000m, 420000m
            };

            var reviewUsers = new[] { "Dr. Adebayo (Medical Reviewer)", "Mrs. Fatima Yusuf (Claims Officer)", "Dr. Chukwu (HMO Auditor)" };
            var approvalUsers = new[] { "Mr. Ibrahim Sani (Finance)", "Mrs. Ngozi Okonkwo (Director Claims)" };
            var paymentUsers = new[] { "Payment Gateway", "Bank Transfer (GTB)", "NHIA Bulk Payment" };

            var rejectionReasons = new[]
            {
               "Missing pre-authorization", "Diagnosis not covered under benefit package",
               "Duplicate claim detected", "Exceeded annual limit", "Documentation incomplete",
               "Provider not accredited for procedure", "Claim submitted after 30-day window"
            };

            var claims = new List<Claim>();

            foreach (var enrollee in enrollees.Take(500))
            {
                int claimCount = random.Next(1, 6); // 1–5 claims per enrollee

                for (int i = 0; i < claimCount; i++)
                {
                    var provider = providers[random.Next(providers.Count)];
                    var diagIndex = random.Next(diagnoses.Length);
                    var baseAmount = baseAmounts[diagIndex];
                    var variation = random.Next(-15000, 35000);
                    var amount = baseAmount + variation;

                    var submittedDate = enrollee.DateRegistered.AddDays(random.Next(30, 730));
                    var statusRoll = random.Next(100);

                    var status = statusRoll switch
                    {
                        < 10 => "Rejected",
                        < 35 => "Pending",
                        < 70 => "Approved",
                        _ => "Paid"
                    };

                    var claim = new Claim
                    {
                        ClaimNumber = $"CLM-{submittedDate:yyyyMMdd}-{random.Next(10000, 99999)}",
                        EnrolleeId = enrollee.Id,
                        ProviderId = provider.Id,
                        Diagnosis = diagnoses[diagIndex],
                        Treatment = treatments[diagIndex],
                        Amount = amount,
                        DateSubmitted = submittedDate,
                        SubmittedBy = provider.Name,
                        Status = status
                    };

                    claim.HmoId = enrollee.HmoId;

                    // FULL AUDIT TRAIL BASED ON STATUS
                    if (status == "Rejected")
                    {
                        claim.DateProcessed = submittedDate.AddDays(random.Next(3, 15));
                        claim.ReviewedBy = reviewUsers[random.Next(reviewUsers.Length)];
                        claim.DateReviewed = claim.DateProcessed.Value.AddHours(random.Next(1, 48));
                        claim.RejectionReason = rejectionReasons[random.Next(rejectionReasons.Length)];
                        claim.RejectedBy = claim.ReviewedBy;
                        claim.DateRejected = claim.DateReviewed;
                    }
                    else if (status == "Approved" || status == "Paid")
                    {
                        claim.DateProcessed = submittedDate.AddDays(random.Next(7, 30));
                        claim.ReviewedBy = reviewUsers[random.Next(reviewUsers.Length)];
                        claim.DateReviewed = claim.DateProcessed.Value.AddHours(random.Next(1, 24));
                        claim.ReviewNotes = "Claim valid and within benefit package.";

                        claim.ApprovedBy = approvalUsers[random.Next(approvalUsers.Length)];
                        claim.DateApproved = claim.DateReviewed.Value.AddDays(random.Next(1, 10));
                        claim.ApprovalNotes = "Approved for payment.";

                        if (status == "Paid")
                        {
                            claim.DatePaid = claim.DateApproved.Value.AddDays(random.Next(14, 90));
                            claim.PaidBy = paymentUsers[random.Next(paymentUsers.Length)];
                            claim.PaymentReference = $"PAY Ctship-{claim.DatePaid:yyyyMM}-{random.Next(1000, 9999)}";
                        }
                    }
                    else
                    {
                        claim.ReviewNotes = "Under review by claims team.";
                    }

                    claims.Add(claim);
                }
            }

            context.Claims.AddRange(claims);
            context.SaveChanges();

            Console.WriteLine($"CLAIMS SEED COMPLETE: {claims.Count} REAL NIGERIAN CLAIMS!");
            Console.WriteLine($"   Paid: {claims.Count(c => c.Status == "Paid")} | Approved: {claims.Count(c => c.Status == "Approved")} | Rejected: {claims.Count(c => c.Status == "Rejected")} | Pending: {claims.Count(c => c.Status == "Pending")}");
            Console.WriteLine("   Full audit trail: ReviewedBy, ApprovedBy, PaidBy, RejectionReason — ALL INCLUDED!");
        }

        private static string[] GetNigerianStates() => new[]
       {
            "Adamawa","Bauchi","Borno",
            "Gombe",  "Taraba", "Yobe"
        };

        private static string[] GetLGAsForState(string state) => state switch
        {
            "Borno" => new[] { "Bama", "Mafa", "Jere", "Lagos Island", "Surulere", "Agege", "Oshodi-Isolo" },
            "Yobe" => new[] { "Abaji", "Bwari", "Gwagwalada", "Kuje", "Kwali", "Municipal" },
            "Adamawa" => new[] { "Municipal", "Fagge", "Dala", "Gwale", "Tarauni" },
            "Gombe" => new[] { "Municipal", "Fagge", "Dala", "Gwale", "Tarauni" },
            "Taraba" => new[] { "Municipal", "Fagge", "Dala", "Gwale", "Tarauni" },
            "Bauchi" => new[] { "Tureta", "Katagum", "Bauchi-North", "Yankari" },
            _ => new[] { "Central LGA", "North LGA", "South LGA" }
        };

        private static string GetRandomStreet()
        {
            var streets = new[] { "Ahmadu Bello Way", "Herbert Macaulay", "Obafemi Awolowo", "Nnamdi Azikiwe",
                          "Tafawa Balewa", "Murtala Muhammed", "Airport Road", "Ring Road", "Sani Abacha" };
            return streets[new Random().Next(streets.Length)];
        }

        private static string GetRandomCity(string state) => state switch
        {
            "Lagos" => new[] { "Ikeja", "Victoria Island", "Lekki", "Surulere", "Yaba" }[new Random().Next(5)],
            "FCT" => new[] { "Garki", "Wuse", "Maitama", "Asokoro", "Gwagwalada" }[new Random().Next(5)],
            "Kano" => new[] { "Kano Municipal", "Fagge", "Nassarawa", "Dala" }[new Random().Next(4)],
            "Rivers" => new[] { "Port Harcourt", "Obio-Akpor", "Eleme" }[new Random().Next(3)],
            "Oyo" => new[] { "Ibadan", "Ogbomosho", "Oyo Town" }[new Random().Next(3)],
            _ => state + " City"
        };

        private static string GetRandomLGA(string state)
        {
            var lgas = state switch
            {
                "Lagos" => new[] { "Ikeja", "Alimosho", "Eti-Osa", "Kosofe", "Lagos Island" },
                "FCT" => new[] { "Abuja Municipal", "Bwari", "Gwagwalada", "Kuje" },
                "Kano" => new[] { "Kano Municipal", "Fagge", "Dala", "Tarauni" },
                _ => new[] { "Central", "North", "South", "East", "West" }
            };
            return lgas[new Random().Next(lgas.Length)] + " LGA";
        }

        private static string GetRandomWard(string state)
        {
            var lgas = state switch
            {
                "Yobe" => new[] { "Ikeja", "Alimosho", "Eti-Osa", "Kosofe", "Lagos Island" },
                "Borno" => new[] { "Abuja Municipal", "Bwari", "Gwagwalada", "Kuje" },
                "Adamawa" => new[] { "Kano Municipal", "Fagge", "Dala", "Tarauni" },
                _ => new[] { "Central", "North", "South", "East", "West" }
            };
            return lgas[new Random().Next(lgas.Length)] + " LGA";
        }
        
        private static string GenerateEnrollmentNumber(string state, int index, Random rand)
        {
            string stateCode = state.ToUpper() switch
            {
                "ADAMAWA" => "AD",
                "BORNO" => "BO",
                "GOMBE" => "GO",
                "TARABA" => "TA",
                "YOBE" => "YO",
                _ => "NG"
            };

            int year = DateTime.Now.Year - rand.Next(0, 3);
            return $"CTSHIP-{year}-{stateCode}-{index + 100000:D6}";
        }

        // Helper: Get Nigerian state code
        private static string GetStateCode(string state)
        {
            return state switch
            {
                "Taraba" => "TR",
                "Borno" => "BN",
                "Yobe" => "YB",
                "Gombe" => "GB",
                "Adamawa" => "AD",
                "Bauchi" => "BC",
                _ => "NG"
            };
        }

        // Random element extension
        private static T RandomElement<T>(this T[] array) => array[new Random().Next(array.Length)];
    }
}


