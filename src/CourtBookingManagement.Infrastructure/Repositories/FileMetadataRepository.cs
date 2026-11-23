using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Files;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class FileMetadataRepository(ISqlConnectionFactory sqlConnectionFactory)
    : IFileMetadataRepository
{
    public async Task<Guid> CreateAsync(
        CreateFileMetadataRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO media.files
            (
                uploader_user_id,
                category,
                cloudinary_public_id,
                secure_url,
                file_name, 
                content_type,
                size_bytes,
                status,
                created_at,
                updated_at
            )
            VALUES
            (
                @UploaderUserId,
                @Category,
                @CloudinaryPublicId,
                @SecureUrl,
                @FileName,
                @ContentType,
                @SizeBytes,
                @Status,
                NOW(),
                NOW()
            )
            RETURNING id;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            sql,
            request,
            cancellationToken: cancellationToken));
    }

    public async Task<FileMetadataRecord?> GetByIdAsync(
        Guid fileId,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                uploader_user_id AS UploaderUserId,
                category AS Category,
                secure_url AS SecureUrl,
                file_name AS FileName,
                content_type AS ContentType,
                size_bytes AS SizeBytes,
                status AS Status,
                deleted_at IS NOT NULL AS IsDeleted
            FROM media.files
            WHERE id = @FileId
            """;

        var query = transaction is null
            ? sql
            : $"{sql}\nFOR UPDATE";
        var command = new CommandDefinition(
            query,
            new { FileId = fileId },
            transaction,
            cancellationToken: cancellationToken);

        if (transaction is not null)
        {
            return await transaction.Connection!.QuerySingleOrDefaultAsync<FileMetadataRecord>(command);
        }

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<FileMetadataRecord>(command);
    }
}
