SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID('tempdb..#ExcessEnrollees') IS NOT NULL
    DROP TABLE #ExcessEnrollees;

DECLARE @ClaimedEnrolleeCount INT =
(
    SELECT COUNT(*)
    FROM
    (
        SELECT claim.EnrolleeId
        FROM dbo.Claims AS claim
        WHERE claim.EnrolleeId IS NOT NULL

        UNION

        SELECT encounter.EnrolleeId
        FROM dbo.Encounters AS encounter
        WHERE encounter.ClaimId IS NOT NULL
    ) AS protectedEnrollees
);

IF @ClaimedEnrolleeCount > 100
BEGIN
    THROW 51000, 'More than 100 enrollees have claims. Cleanup stopped to protect all claim history.', 1;
END;

;WITH EnrolleeActivity AS
(
    SELECT
        enrollee.Id,
        enrollee.DateRegistered,
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM dbo.Claims AS claim
            WHERE claim.EnrolleeId = enrollee.Id
        )
        OR EXISTS
        (
            SELECT 1
            FROM dbo.Encounters AS encounter
            WHERE encounter.EnrolleeId = enrollee.Id
                AND encounter.ClaimId IS NOT NULL
        ) THEN 1 ELSE 0 END AS HasClaim,
        (
            SELECT COUNT(*)
            FROM dbo.Claims AS claim
            WHERE claim.EnrolleeId = enrollee.Id
        )
        +
        (
            SELECT COUNT(*)
            FROM dbo.Encounters AS encounter
            WHERE encounter.EnrolleeId = enrollee.Id
                AND encounter.ClaimId IS NOT NULL
        ) AS ClaimCount,
        (
            SELECT COUNT(*)
            FROM dbo.Encounters AS encounter
            WHERE encounter.EnrolleeId = enrollee.Id
        ) AS EncounterCount
    FROM dbo.Enrollees AS enrollee
),
RankedEnrollees AS
(
    SELECT
        Id,
        ROW_NUMBER() OVER
        (
            ORDER BY
                HasClaim DESC,
                ClaimCount DESC,
                EncounterCount DESC,
                DateRegistered ASC,
                Id ASC
        ) AS RowNumber
    FROM EnrolleeActivity
)
SELECT Id
INTO #ExcessEnrollees
FROM RankedEnrollees
WHERE RowNumber > 100;

IF EXISTS
(
    SELECT 1
    FROM dbo.Claims AS claim
    INNER JOIN #ExcessEnrollees AS excess
        ON excess.Id = claim.EnrolleeId
)
OR EXISTS
(
    SELECT 1
    FROM dbo.Encounters AS encounter
    INNER JOIN #ExcessEnrollees AS excess
        ON excess.Id = encounter.EnrolleeId
    WHERE encounter.ClaimId IS NOT NULL
)
BEGIN
    THROW 51001, 'Cleanup selection included an enrollee with claims. No records were deleted.', 1;
END;

DECLARE @BeforeCount INT = (SELECT COUNT(*) FROM dbo.Enrollees);
DECLARE @DeleteCount INT = (SELECT COUNT(*) FROM #ExcessEnrollees);

IF @DeleteCount > 0
BEGIN
    IF OBJECT_ID('dbo.DeathRegisterAuditLogs', 'U') IS NOT NULL
    BEGIN
        DELETE auditLog
        FROM dbo.DeathRegisterAuditLogs AS auditLog
        INNER JOIN dbo.DeathRegisters AS deathRegister
            ON deathRegister.Id = auditLog.DeathRegisterId
        INNER JOIN #ExcessEnrollees AS excess
            ON excess.Id = deathRegister.EnrolleeId;
    END;

    IF OBJECT_ID('dbo.DeathRegisters', 'U') IS NOT NULL
    BEGIN
        DELETE deathRegister
        FROM dbo.DeathRegisters AS deathRegister
        INNER JOIN #ExcessEnrollees AS excess
            ON excess.Id = deathRegister.EnrolleeId;
    END;

    IF OBJECT_ID('dbo.EncounterServices', 'U') IS NOT NULL
    BEGIN
        DELETE service
        FROM dbo.EncounterServices AS service
        INNER JOIN dbo.Encounters AS encounter
            ON encounter.Id = service.EncounterId
        INNER JOIN #ExcessEnrollees AS excess
            ON excess.Id = encounter.EnrolleeId;
    END;

    IF OBJECT_ID('dbo.Encounters', 'U') IS NOT NULL
    BEGIN
        DELETE encounter
        FROM dbo.Encounters AS encounter
        INNER JOIN #ExcessEnrollees AS excess
            ON excess.Id = encounter.EnrolleeId;
    END;

    IF OBJECT_ID('dbo.Claims', 'U') IS NOT NULL
    BEGIN
        DELETE claim
        FROM dbo.Claims AS claim
        INNER JOIN #ExcessEnrollees AS excess
            ON excess.Id = claim.EnrolleeId;
    END;

    IF OBJECT_ID('dbo.Feedbacks', 'U') IS NOT NULL
    BEGIN
        DELETE feedback
        FROM dbo.Feedbacks AS feedback
        INNER JOIN #ExcessEnrollees AS excess
            ON excess.Id = feedback.EnrolleeId;
    END;

    IF OBJECT_ID('dbo.MedicalHistories', 'U') IS NOT NULL
    BEGIN
        DELETE history
        FROM dbo.MedicalHistories AS history
        INNER JOIN #ExcessEnrollees AS excess
            ON excess.Id = history.EnrolleeId;
    END;

    DELETE enrollee
    FROM dbo.Enrollees AS enrollee
    INNER JOIN #ExcessEnrollees AS excess
        ON excess.Id = enrollee.Id;
END;

DECLARE @AfterCount INT = (SELECT COUNT(*) FROM dbo.Enrollees);

COMMIT TRANSACTION;

SELECT
    @BeforeCount AS EnrolleesBefore,
    @DeleteCount AS EnrolleesDeleted,
    @AfterCount AS EnrolleesAfter,
    @ClaimedEnrolleeCount AS ClaimedEnrolleesProtected;
