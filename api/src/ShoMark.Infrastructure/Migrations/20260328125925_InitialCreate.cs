using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoMark.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "videos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    minio_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    duration_seconds = table.Column<double>(type: "double precision", nullable: true),
                    file_size = table.Column<long>(type: "bigint", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_videos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platforms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    account_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    access_token = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    refresh_token = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platforms", x => x.id);
                    table.ForeignKey(
                        name: "FK_platforms_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_fragments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    video_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    start_time = table.Column<double>(type: "double precision", nullable: false),
                    end_time = table.Column<double>(type: "double precision", nullable: false),
                    minio_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    viral_score = table.Column<double>(type: "double precision", nullable: true),
                    hashtags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    thumbnail_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_fragments", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_fragments_videos_video_id",
                        column: x => x.video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaigns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fragment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    video_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    target_audience = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaigns", x => x.id);
                    table.ForeignKey(
                        name: "FK_campaigns_ai_fragments_fragment_id",
                        column: x => x.fragment_id,
                        principalTable: "ai_fragments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_campaigns_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_campaigns_videos_video_id",
                        column: x => x.video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "fragment_tags",
                columns: table => new
                {
                    fragment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fragment_tags", x => new { x.fragment_id, x.tag_id });
                    table.ForeignKey(
                        name: "FK_fragment_tags_ai_fragments_fragment_id",
                        column: x => x.fragment_id,
                        principalTable: "ai_fragments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fragment_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    fragment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    content = table.Column<string>(type: "text", nullable: true),
                    external_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posts", x => x.id);
                    table.ForeignKey(
                        name: "FK_posts_ai_fragments_fragment_id",
                        column: x => x.fragment_id,
                        principalTable: "ai_fragments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_posts_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalTable: "campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_posts_platforms_platform_id",
                        column: x => x.platform_id,
                        principalTable: "platforms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "analytics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    views = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    likes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    shares = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    comments = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics", x => x.id);
                    table.ForeignKey(
                        name: "FK_analytics_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_fragments_video_id",
                table: "ai_fragments",
                column: "video_id");

            migrationBuilder.CreateIndex(
                name: "IX_analytics_post_id",
                table: "analytics",
                column: "post_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_fragment_id",
                table: "campaigns",
                column: "fragment_id");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_user_id",
                table: "campaigns",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_video_id",
                table: "campaigns",
                column: "video_id");

            migrationBuilder.CreateIndex(
                name: "IX_fragment_tags_fragment_id",
                table: "fragment_tags",
                column: "fragment_id");

            migrationBuilder.CreateIndex(
                name: "IX_fragment_tags_tag_id",
                table: "fragment_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "IX_platforms_token_expires_at",
                table: "platforms",
                column: "token_expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_platforms_user_id",
                table: "platforms",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_posts_campaign_id",
                table: "posts",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "IX_posts_fragment_id",
                table: "posts",
                column: "fragment_id");

            migrationBuilder.CreateIndex(
                name: "IX_posts_platform_id",
                table: "posts",
                column: "platform_id");

            migrationBuilder.CreateIndex(
                name: "IX_posts_scheduled_at",
                table: "posts",
                column: "scheduled_at");

            migrationBuilder.CreateIndex(
                name: "IX_posts_status",
                table: "posts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_tags_name",
                table: "tags",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tags_slug",
                table: "tags",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_videos_minio_key",
                table: "videos",
                column: "minio_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics");

            migrationBuilder.DropTable(
                name: "fragment_tags");

            migrationBuilder.DropTable(
                name: "posts");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "campaigns");

            migrationBuilder.DropTable(
                name: "platforms");

            migrationBuilder.DropTable(
                name: "ai_fragments");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "videos");
        }
    }
}
