using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoMark.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameMinioKeyToStorageKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "minio_key",
                table: "videos",
                newName: "storage_key");

            migrationBuilder.RenameIndex(
                name: "IX_videos_minio_key",
                table: "videos",
                newName: "IX_videos_storage_key");

            migrationBuilder.RenameColumn(
                name: "minio_key",
                table: "ai_fragments",
                newName: "storage_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "storage_key",
                table: "videos",
                newName: "minio_key");

            migrationBuilder.RenameIndex(
                name: "IX_videos_storage_key",
                table: "videos",
                newName: "IX_videos_minio_key");

            migrationBuilder.RenameColumn(
                name: "storage_key",
                table: "ai_fragments",
                newName: "minio_key");
        }
    }
}
