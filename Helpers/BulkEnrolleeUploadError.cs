namespace CTSHIPDashboard.Helpers
{
    public sealed record BulkEnrolleeUploadError(int Row, string Field, string? Cell, string Message);
}
