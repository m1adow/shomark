# AGENT.md - ShoMark: Video Highlight Extraction & Campaign Management System

## 🤖 Agent Identity
**Name:** UPIT Event Promotion Agent
**Role:** AI-Powered Video Intelligence & Campaign Coordinator
**Mission:** To assist the Department of Project Management in IT in converting raw event recordings into audience-targeted short-form video clips, then organizing those clips into multi-platform marketing campaigns for three target segments: applicants, prospective masters, and IT professionals.

## 🎯 Target Audience Segmentation (5W Framework)
The system applies audience-aware AI prompts across three primary segments when extracting video highlights:

1. **Applicants (Bachelors):** Clips emphasizing student life, career entry, and emotional engagement — optimized for Instagram and TikTok (9:16 vertical format).
2. **Prospective Masters:** Clips highlighting career growth, research impact, and professional development — targeted at LinkedIn and YouTube.
3. **IT Professionals:** Clips focused on niche technical content, workshops, and certifications — distributed via professional community channels.

## 🧠 Core Capabilities & Skills

### 1. AI Video Intelligence (Implemented)
* **Transcription:** Uses **faster-whisper** to generate a full transcript of uploaded event recordings (supports large video files up to 2 GB).
* **Highlight Detection:** Sends the transcript to a locally-hosted **Ollama** LLM with audience-specific prompts to identify the most compelling segments per persona. Each highlight is returned with: `start_time`, `end_time`, `title`, `reason`, `viral_score`, and `hashtags`.
* **Clip Generation:** Cuts detected segments into standalone 9:16 vertical video clips using **FFmpeg**, optimized for short-form platform formats (TikTok, Reels).
* **Async Pipeline:** The full 5-step pipeline (Download → Transcribe → Detect → Cut → Upload) runs asynchronously via a Kafka consumer worker. Horizontal scaling is supported through Kafka consumer groups.

### 2. Audience-Aware Content Targeting (Implemented)
* **Per-Persona Prompting:** Ollama highlight detection prompts are parameterized per target audience segment (Applicants, Masters, IT Professionals), ensuring extracted clips resonate with the intended viewer.
* **Structured Output:** Each AI fragment includes metadata — description, start/end timestamps, viral score, and tags — enabling reviewers to prioritize the most impactful clips.
* **Human-in-the-Loop Review:** All AI-generated fragments enter a manual approval step before entering any campaign, maintaining brand voice and content quality.

### 3. Campaign & Post Management (Implemented)
* **Fragment Approval Workflow:** Users review AI-extracted fragments, approve or discard clips, and assign approved fragments to campaigns.
* **Campaign Lifecycle:** Campaigns progress through four states: Draft → Active → Completed → Archived.
* **Post Scheduling:** Posts are created from fragments and scheduled for publishing across five platforms: Instagram, TikTok, YouTube, X, and LinkedIn. Posts are auto-set to `Scheduled` status when a future publish time is provided.
* **Analytics Tracking:** Post-level analytics (views, likes, shares, comments) are stored per post and synced manually. A centralized analytics page aggregates KPIs across campaigns.
* **OAuth Integration:** Platform accounts are connected via OAuth 2.0 flows for all five supported platforms.

## 🛠 Technical Stack

* **Frontend:** React 18 + TypeScript, PrimeReact component library, Tailwind CSS, Vite.
* **Backend API:** .NET 10 (ASP.NET Core), Clean 4-layer architecture (Domain / Application / Infrastructure / API), Entity Framework Core 10.0.3.
* **Worker:** Python, faster-whisper (transcription), Ollama (LLM highlight detection), FFmpeg (video cutting), concurrent processing with configurable parallelism.
* **Database:** PostgreSQL with Npgsql EF Core provider. 10 entities with full fluent API configuration.
* **Messaging:** Apache Kafka (KRaft mode, no Zookeeper). Topics: `video-processing` (trigger) and `video-processing-completed` (result).
* **Storage:** MinIO (S3-compatible) for original videos, processed clips, and thumbnails. Presigned URLs for secure client access.
* **Authentication:** Keycloak (JWT Bearer). All API endpoints require authentication.
* **Infrastructure:** Docker & Docker Compose for all services.
* **CI/CD:** GitHub Actions.

## ⚖️ Operational Guidelines & Ethics
* **Privacy by Design:** Adheres to **GDPR** standards to protect user data.
* **Quality Control:** All AI-generated fragments pass through a human review and approval step before being used in campaigns.
* **Bias Mitigation:** Audience-specific prompts are reviewed regularly to ensure balanced and fair content extraction across all three target segments.

## 📈 Performance Metrics (KPIs)
* **Pipeline Throughput:** Video processing pipeline completes asynchronously; throughput scales horizontally via Kafka consumer group parallelism.
* **Fragment Review Cycle:** Time from video upload to approved fragments available for campaigns.
* **Reliability:** System availability target of **99.5%**.
* **Success:** Measured by campaign reach, post engagement rates, and conversion of event viewers into enrolled students.