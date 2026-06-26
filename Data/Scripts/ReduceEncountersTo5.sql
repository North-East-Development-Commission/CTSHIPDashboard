SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TargetEncounterCount INT = 5;

BEGIN TRY
    BEGIN TRANSACTION;

    IF @TargetEncounterCount < 1
    BEGIN
        THROW 51010, 'Target encounter count must be at least 1.', 1;
    END;

    IF OBJECT_ID('dbo.Encounters', 'U') IS NULL
    BEGIN
        THROW 51011, 'Encounters table was not found. Cleanup stopped.', 1;
    END;

    IF OBJECT_ID('dbo.Claims', 'U') IS NULL
    BEGIN
        THROW 51012, 'Claims table was not found. Cleanup stopped.', 1;
    END;

    IF OBJECT_ID('tempdb..#ExcessEncounters') IS NOT NULL
        DROP TABLE #ExcessEncounters;

    IF OBJECT_ID('tempdb..#ClaimsToDelete') IS NOT NULL
        DROP TABLE #ClaimsToDelete;

    CREATE TABLE #ExcessEncounters
    (
        Id INT NOT NULL PRIMARY KEY,
        ClaimId INT NULL
    );

    CREATE TABLE #ClaimsToDelete
    (
        Id INT NOT NULL PRIMARY KEY
    );

    ;WITH RankedEncounters AS
    (
        SELECT
            encounter.Id,
            encounter.ClaimId,
            ROW_NUMBER() OVER
            (
                ORDER BY
                    encounter.VisitDate DESC,
                    encounter.Id DESC
            ) AS RowNumber
        FROM dbo.Encounters AS encounter WITH (UPDLOCK, HOLDLOCK)
    )
    INSERT INTO #ExcessEncounters (Id, ClaimId)
    SELECT Id, ClaimId
    FROM RankedEncounters
    WHERE RowNumber > @TargetEncounterCount;

    INSERT INTO #ClaimsToDelete (Id)
    SELECT DISTINCT excess.ClaimId
    FROM #ExcessEncounters AS excess
    WHERE excess.ClaimId IS NOT NULL
        AND NOT EXISTS
        (
            SELECT 1
            FROM dbo.Encounters AS remainingEncounter
            WHERE remainingEncounter.ClaimId = excess.ClaimId
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM #ExcessEncounters AS deletedEncounter
                    WHERE deletedEncounter.Id = remainingEncounter.Id
                )
        );

    DECLARE @BeforeEncounterCount INT = (SELECT COUNT(*) FROM dbo.Encounters);
    DECLARE @BeforeClaimCount INT = (SELECT COUNT(*) FROM dbo.Claims);
    DECLARE @EncounterDeleteCount INT = (SELECT COUNT(*) FROM #ExcessEncounters);
    DECLARE @ClaimDeleteCount INT = (SELECT COUNT(*) FROM #ClaimsToDelete);

    IF @EncounterDeleteCount > 0
    BEGIN
        IF OBJECT_ID('dbo.EncounterServices', 'U') IS NOT NULL
        BEGIN
            DELETE service
            FROM dbo.EncounterServices AS service
            INNER JOIN #ExcessEncounters AS excess
                ON excess.Id = service.EncounterId;
        END;

        DELETE encounter
        FROM dbo.Encounters AS encounter
        INNER JOIN #ExcessEncounters AS excess
            ON excess.Id = encounter.Id;

        DELETE claim
        FROM dbo.Claims AS claim
        INNER JOIN #ClaimsToDelete AS excessClaim
            ON excessClaim.Id = claim.Id;
    END;

    DECLARE @AfterEncounterCount INT = (SELECT COUNT(*) FROM dbo.Encounters);
    DECLARE @AfterClaimCount INT = (SELECT COUNT(*) FROM dbo.Claims);
    DECLARE @ExpectedEncounterCount INT =
        CASE
            WHEN @BeforeEncounterCount > @TargetEncounterCount THEN @TargetEncounterCount
            ELSE @BeforeEncounterCount
        END;

    IF @AfterEncounterCount <> @ExpectedEncounterCount
    BEGIN
        THROW 51013, 'Encounter cleanup did not finish at the expected count. Changes were rolled back.', 1;
    END;

    COMMIT TRANSACTION;

    SELECT
        @BeforeEncounterCount AS EncountersBefore,
        @EncounterDeleteCount AS EncountersDeleted,
        @AfterEncounterCount AS EncountersAfter,
        @BeforeClaimCount AS ClaimsBefore,
        @ClaimDeleteCount AS ClaimsDeleted,
        @AfterClaimCount AS ClaimsAfter;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
