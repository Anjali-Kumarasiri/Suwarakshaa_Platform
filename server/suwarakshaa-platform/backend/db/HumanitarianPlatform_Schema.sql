/* =====================================================================
   SE205.3 - AI-Powered Humanitarian Crisis Management Platform
   SQL Server schema (T-SQL)
   Sections:
     1  Identity & access        7  Volunteers
     2  Crisis & areas           8  Donors & donations
     3  Citizens & requests      9  Notifications & outbox (async)
     4  AI & human governance   10  Audit, KPIs, mapping
     5  Shelters                11  Indexes
     6  Resources & logistics   12  Seed data
   ===================================================================== */

IF DB_ID('HumanitarianPlatform') IS NULL CREATE DATABASE HumanitarianPlatform;
GO
USE HumanitarianPlatform;
GO

/* ---------- 1. IDENTITY & ACCESS ---------- */
CREATE TABLE Organisations (
    OrganisationId   INT IDENTITY PRIMARY KEY,
    Name             NVARCHAR(200) NOT NULL,
    Type             NVARCHAR(30)  NOT NULL CHECK (Type IN ('Government','NGO','International','Partner')),
    Country          NVARCHAR(100) NULL,
    ContactEmail     NVARCHAR(256) NULL,
    IsActive         BIT NOT NULL DEFAULT 1,
    CreatedAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Roles (
    RoleId   INT IDENTITY PRIMARY KEY,
    Name     NVARCHAR(60) NOT NULL UNIQUE,
    Description NVARCHAR(250) NULL
);

CREATE TABLE Permissions (
    PermissionId INT IDENTITY PRIMARY KEY,
    Code         NVARCHAR(80) NOT NULL UNIQUE,   -- e.g. REQUEST_APPROVE, AI_OVERRIDE
    Description  NVARCHAR(250) NULL
);

CREATE TABLE RolePermissions (
    RoleId       INT NOT NULL REFERENCES Roles(RoleId),
    PermissionId INT NOT NULL REFERENCES Permissions(PermissionId),
    PRIMARY KEY (RoleId, PermissionId)
);

CREATE TABLE Users (
    UserId         INT IDENTITY PRIMARY KEY,
    OrganisationId INT NULL REFERENCES Organisations(OrganisationId),
    Email          NVARCHAR(256) NOT NULL UNIQUE,
    PasswordHash   NVARCHAR(512) NOT NULL,        -- BCrypt/PBKDF2/Argon2 hash, never plain text
    FullName       NVARCHAR(200) NOT NULL,
    Phone          NVARCHAR(30)  NULL,
    PreferredLanguage NVARCHAR(10) NOT NULL DEFAULT 'en',
    IsActive       BIT NOT NULL DEFAULT 1,
    FailedLoginCount INT NOT NULL DEFAULT 0,
    LockoutUntil   DATETIME2 NULL,
    CreatedAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE UserRoles (
    UserId INT NOT NULL REFERENCES Users(UserId),
    RoleId INT NOT NULL REFERENCES Roles(RoleId),
    PRIMARY KEY (UserId, RoleId)
);

CREATE TABLE RefreshTokens (
    TokenId   BIGINT IDENTITY PRIMARY KEY,
    UserId    INT NOT NULL REFERENCES Users(UserId),
    TokenHash NVARCHAR(256) NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    RevokedAt DATETIME2 NULL
);

/* ---------- 2. CRISIS & OPERATIONAL AREAS ---------- */
CREATE TABLE CrisisEvents (
    CrisisId    INT IDENTITY PRIMARY KEY,
    Name        NVARCHAR(200) NOT NULL,
    Type        NVARCHAR(40)  NOT NULL,   -- Flood, Landslide, Earthquake, Storm, Outbreak...
    Severity    TINYINT NOT NULL CHECK (Severity BETWEEN 1 AND 5),
    Status      NVARCHAR(20) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Planned','Active','Contained','Closed')),
    StartedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ClosedAt    DATETIME2 NULL,
    CreatedBy   INT NOT NULL REFERENCES Users(UserId)
);

CREATE TABLE CrisisOrganisations (   -- multiple organisations on the same crisis
    CrisisId       INT NOT NULL REFERENCES CrisisEvents(CrisisId),
    OrganisationId INT NOT NULL REFERENCES Organisations(OrganisationId),
    RoleInCrisis   NVARCHAR(100) NULL,
    PRIMARY KEY (CrisisId, OrganisationId)
);

CREATE TABLE OperationalAreas (
    AreaId    INT IDENTITY PRIMARY KEY,
    CrisisId  INT NOT NULL REFERENCES CrisisEvents(CrisisId),
    Name      NVARCHAR(200) NOT NULL,
    CentreLat DECIMAL(9,6) NOT NULL,
    CentreLng DECIMAL(9,6) NOT NULL,
    RadiusKm  DECIMAL(8,2) NOT NULL,
    BoundaryGeoJson NVARCHAR(MAX) NULL
);

/* ---------- 3. CITIZENS & ASSISTANCE REQUESTS ---------- */
CREATE TABLE Households (
    HouseholdId  INT IDENTITY PRIMARY KEY,
    UserId       INT NOT NULL REFERENCES Users(UserId),
    MemberCount  INT NOT NULL CHECK (MemberCount > 0),
    HasChildren  BIT NOT NULL DEFAULT 0,
    HasElderly   BIT NOT NULL DEFAULT 0,
    HasDisability BIT NOT NULL DEFAULT 0,
    MedicalNotesEnc VARBINARY(MAX) NULL,      -- encrypted sensitive data
    AddressEnc   VARBINARY(MAX) NULL
);

CREATE TABLE AssistanceRequests (
    RequestId     BIGINT IDENTITY PRIMARY KEY,
    ReferenceNo   NVARCHAR(30) NOT NULL UNIQUE,
    CrisisId      INT NOT NULL REFERENCES CrisisEvents(CrisisId),
    AreaId        INT NULL REFERENCES OperationalAreas(AreaId),
    HouseholdId   INT NULL REFERENCES Households(HouseholdId),
    SubmittedBy   INT NOT NULL REFERENCES Users(UserId),
    Category      NVARCHAR(30) NOT NULL CHECK (Category IN ('Food','Medicine','Shelter','Transport','Accessibility','Water','Other')),
    Description   NVARCHAR(MAX) NOT NULL,
    Language      NVARCHAR(10) NOT NULL DEFAULT 'en',
    PeopleAffected INT NOT NULL DEFAULT 1,
    Latitude      DECIMAL(9,6) NULL,
    Longitude     DECIMAL(9,6) NULL,
    FormattedAddress NVARCHAR(400) NULL,
    UserUrgency   TINYINT NULL CHECK (UserUrgency BETWEEN 1 AND 5),
    FinalPriority TINYINT NULL CHECK (FinalPriority BETWEEN 1 AND 5),  -- set by human decision
    Status        NVARCHAR(20) NOT NULL DEFAULT 'Submitted'
        CHECK (Status IN ('Submitted','UnderReview','Verified','Escalated','Assigned','InProgress','Fulfilled','Rejected','Merged','Closed')),
    AssignedOrgId INT NULL REFERENCES Organisations(OrganisationId),
    AssignedTo    INT NULL REFERENCES Users(UserId),
    IsFlaggedSuspicious BIT NOT NULL DEFAULT 0,
    DueAt         DATETIME2 NULL,
    CreatedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    FulfilledAt   DATETIME2 NULL,
    RowVer        ROWVERSION                  -- optimistic concurrency
);

CREATE TABLE RequestHistory (        -- timeline of status changes / decisions
    HistoryId   BIGINT IDENTITY PRIMARY KEY,
    RequestId   BIGINT NOT NULL REFERENCES AssistanceRequests(RequestId),
    FromStatus  NVARCHAR(20) NULL,
    ToStatus    NVARCHAR(20) NOT NULL,
    ChangedBy   INT NULL REFERENCES Users(UserId),   -- NULL = system
    Note        NVARCHAR(1000) NULL,
    ChangedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE RequestAttachments (
    AttachmentId BIGINT IDENTITY PRIMARY KEY,
    RequestId    BIGINT NOT NULL REFERENCES AssistanceRequests(RequestId),
    StoredFileName NVARCHAR(260) NOT NULL,           -- server-generated name
    OriginalFileName NVARCHAR(260) NOT NULL,
    ContentType  NVARCHAR(100) NOT NULL,
    SizeBytes    BIGINT NOT NULL,
    Sha256       CHAR(64) NOT NULL,
    UploadedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE DuplicateCandidates (   -- flagged, never auto-deleted
    DuplicateId     BIGINT IDENTITY PRIMARY KEY,
    RequestId       BIGINT NOT NULL REFERENCES AssistanceRequests(RequestId),
    OtherRequestId  BIGINT NOT NULL REFERENCES AssistanceRequests(RequestId),
    SimilarityScore DECIMAL(5,4) NOT NULL CHECK (SimilarityScore BETWEEN 0 AND 1),
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','ConfirmedDuplicate','NotDuplicate','Merged')),
    ReviewedBy      INT NULL REFERENCES Users(UserId),
    ReviewedAt      DATETIME2 NULL,
    CONSTRAINT CK_Dup_Different CHECK (RequestId <> OtherRequestId),
    CONSTRAINT UQ_Dup_Pair UNIQUE (RequestId, OtherRequestId)
);

/* ---------- 4. AI & HUMAN-IN-THE-LOOP GOVERNANCE ---------- */
CREATE TABLE AiRecommendations (
    RecommendationId BIGINT IDENTITY PRIMARY KEY,
    Type            NVARCHAR(30) NOT NULL CHECK (Type IN
        ('Classification','Priority','Duplicate','ResourceMatch','ShelterMatch','VolunteerMatch','Forecast','Anomaly','Summary','Response')),
    RequestId       BIGINT NULL REFERENCES AssistanceRequests(RequestId),
    CrisisId        INT NULL REFERENCES CrisisEvents(CrisisId),
    ModelName       NVARCHAR(100) NOT NULL,
    ModelVersion    NVARCHAR(50)  NULL,
    InputSnapshot   NVARCHAR(MAX) NULL,                -- what the AI saw (minimised/redacted)
    OutputJson      NVARCHAR(MAX) NOT NULL,            -- original AI recommendation
    Confidence      DECIMAL(5,4) NOT NULL CHECK (Confidence BETWEEN 0 AND 1),
    Rationale       NVARCHAR(2000) NULL,
    IsFallback      BIT NOT NULL DEFAULT 0,            -- rule-based fallback when AI is down
    Status          NVARCHAR(20) NOT NULL DEFAULT 'PendingReview'
        CHECK (Status IN ('PendingReview','Approved','Modified','Rejected','Deferred','Expired')),
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE HumanDecisions (        -- one row per human action on an AI recommendation
    DecisionId       BIGINT IDENTITY PRIMARY KEY,
    RecommendationId BIGINT NOT NULL REFERENCES AiRecommendations(RecommendationId),
    Action           NVARCHAR(10) NOT NULL CHECK (Action IN ('Approve','Modify','Reject','Defer')),
    FinalValueJson   NVARCHAR(MAX) NULL,               -- value actually applied
    Reason           NVARCHAR(1000) NULL,
    DecidedBy        INT NOT NULL REFERENCES Users(UserId),
    DecidedAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    RequiresSecondApproval BIT NOT NULL DEFAULT 0,
    SecondApprovedBy INT NULL REFERENCES Users(UserId),
    SecondApprovedAt DATETIME2 NULL,
    CONSTRAINT CK_Decision_Reason CHECK (Action NOT IN ('Modify','Reject') OR Reason IS NOT NULL)
);

/* ---------- 5. SHELTERS ---------- */
CREATE TABLE Shelters (
    ShelterId     INT IDENTITY PRIMARY KEY,
    CrisisId      INT NOT NULL REFERENCES CrisisEvents(CrisisId),
    OrganisationId INT NOT NULL REFERENCES Organisations(OrganisationId),
    AdminUserId   INT NULL REFERENCES Users(UserId),
    Name          NVARCHAR(200) NOT NULL,
    Type          NVARCHAR(20) NOT NULL DEFAULT 'Shelter' CHECK (Type IN ('Shelter','ServiceCentre')),
    Address       NVARCHAR(400) NULL,
    Latitude      DECIMAL(9,6) NOT NULL,
    Longitude     DECIMAL(9,6) NOT NULL,
    Capacity      INT NOT NULL CHECK (Capacity >= 0),
    CurrentOccupancy INT NOT NULL DEFAULT 0,
    IsAccessible  BIT NOT NULL DEFAULT 0,
    IsPublic      BIT NOT NULL DEFAULT 0,               -- visible in citizen portal
    Status        NVARCHAR(20) NOT NULL DEFAULT 'Open' CHECK (Status IN ('Open','Full','Closed')),
    RowVer        ROWVERSION,
    CONSTRAINT CK_Shelter_NoOverAllocation CHECK (CurrentOccupancy >= 0 AND CurrentOccupancy <= Capacity)
);

CREATE TABLE ShelterFacilities (
    ShelterId INT NOT NULL REFERENCES Shelters(ShelterId),
    Facility  NVARCHAR(60) NOT NULL,   -- Medical, Kitchen, Wheelchair access, Family area...
    PRIMARY KEY (ShelterId, Facility)
);

CREATE TABLE ShelterAllocations (
    AllocationId  BIGINT IDENTITY PRIMARY KEY,
    ShelterId     INT NOT NULL REFERENCES Shelters(ShelterId),
    RequestId     BIGINT NOT NULL REFERENCES AssistanceRequests(RequestId),
    HouseholdId   INT NULL REFERENCES Households(HouseholdId),
    PeopleCount   INT NOT NULL CHECK (PeopleCount > 0),
    Status        NVARCHAR(20) NOT NULL DEFAULT 'Allocated' CHECK (Status IN ('Allocated','CheckedIn','CheckedOut','Cancelled')),
    RecommendationId BIGINT NULL REFERENCES AiRecommendations(RecommendationId),
    AllocatedBy   INT NOT NULL REFERENCES Users(UserId),
    AllocatedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CheckedOutAt  DATETIME2 NULL
);

/* ---------- 6. RESOURCES & LOGISTICS ---------- */
CREATE TABLE ResourceTypes (
    ResourceTypeId INT IDENTITY PRIMARY KEY,
    Name     NVARCHAR(100) NOT NULL UNIQUE,   -- Rice pack, Water (L), Ambulance, Medical kit...
    Category NVARCHAR(30) NOT NULL CHECK (Category IN ('Food','Water','Medicine','Vehicle','Personnel','Equipment','Other')),
    Unit     NVARCHAR(20) NOT NULL,
    IsPerishable BIT NOT NULL DEFAULT 0
);

CREATE TABLE ResourceInventories (
    InventoryId    INT IDENTITY PRIMARY KEY,
    ResourceTypeId INT NOT NULL REFERENCES ResourceTypes(ResourceTypeId),
    OrganisationId INT NOT NULL REFERENCES Organisations(OrganisationId),
    CrisisId       INT NULL REFERENCES CrisisEvents(CrisisId),
    LocationName   NVARCHAR(200) NULL,
    Latitude       DECIMAL(9,6) NULL,
    Longitude      DECIMAL(9,6) NULL,
    QuantityTotal     DECIMAL(12,2) NOT NULL DEFAULT 0,
    QuantityAllocated DECIMAL(12,2) NOT NULL DEFAULT 0,
    ReorderLevel      DECIMAL(12,2) NOT NULL DEFAULT 0,
    ExpiryDate     DATE NULL,
    RowVer         ROWVERSION,
    CONSTRAINT CK_Inventory_NoOverAllocation CHECK (QuantityAllocated >= 0 AND QuantityAllocated <= QuantityTotal)
);

CREATE TABLE ResourceAllocations (
    ResourceAllocationId BIGINT IDENTITY PRIMARY KEY,
    InventoryId   INT NOT NULL REFERENCES ResourceInventories(InventoryId),
    RequestId     BIGINT NOT NULL REFERENCES AssistanceRequests(RequestId),
    Quantity      DECIMAL(12,2) NOT NULL CHECK (Quantity > 0),
    Status        NVARCHAR(20) NOT NULL DEFAULT 'Reserved' CHECK (Status IN ('Reserved','Dispatched','Delivered','Cancelled','Preempted')),
    RecommendationId BIGINT NULL REFERENCES AiRecommendations(RecommendationId),
    ConflictGroupId UNIQUEIDENTIFIER NULL,            -- links competing requests in a shortage
    AllocatedBy   INT NOT NULL REFERENCES Users(UserId),
    AllocatedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE DispatchRecords (
    DispatchId   BIGINT IDENTITY PRIMARY KEY,
    ResourceAllocationId BIGINT NOT NULL REFERENCES ResourceAllocations(ResourceAllocationId),
    VehicleInventoryId INT NULL REFERENCES ResourceInventories(InventoryId),
    DriverUserId INT NULL REFERENCES Users(UserId),
    DistanceKm   DECIMAL(8,2) NULL,
    EtaMinutes   INT NULL,
    DispatchedAt DATETIME2 NULL,
    DeliveredAt  DATETIME2 NULL,
    Status       NVARCHAR(20) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','InTransit','Delivered','Failed'))
);

CREATE TABLE PartnerAssistanceRequests (   -- asking partner orgs for help in a shortage
    PartnerRequestId BIGINT IDENTITY PRIMARY KEY,
    FromOrganisationId INT NOT NULL REFERENCES Organisations(OrganisationId),
    ToOrganisationId   INT NOT NULL REFERENCES Organisations(OrganisationId),
    ResourceTypeId INT NOT NULL REFERENCES ResourceTypes(ResourceTypeId),
    Quantity       DECIMAL(12,2) NOT NULL CHECK (Quantity > 0),
    Status         NVARCHAR(20) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','Accepted','Declined','Fulfilled')),
    CreatedAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

/* ---------- 7. VOLUNTEERS ---------- */
CREATE TABLE Skills (
    SkillId INT IDENTITY PRIMARY KEY,
    Name    NVARCHAR(100) NOT NULL UNIQUE
);

CREATE TABLE Volunteers (
    VolunteerId  INT IDENTITY PRIMARY KEY,
    UserId       INT NOT NULL UNIQUE REFERENCES Users(UserId),
    OrganisationId INT NULL REFERENCES Organisations(OrganisationId),
    VerificationStatus NVARCHAR(20) NOT NULL DEFAULT 'Pending' CHECK (VerificationStatus IN ('Pending','Verified','Rejected','Suspended')),
    VerifiedBy   INT NULL REFERENCES Users(UserId),
    BaseLatitude DECIMAL(9,6) NULL,
    BaseLongitude DECIMAL(9,6) NULL,
    HasVehicle   BIT NOT NULL DEFAULT 0
);

CREATE TABLE VolunteerSkills (
    VolunteerId INT NOT NULL REFERENCES Volunteers(VolunteerId),
    SkillId     INT NOT NULL REFERENCES Skills(SkillId),
    Level       TINYINT NOT NULL DEFAULT 1 CHECK (Level BETWEEN 1 AND 5),
    PRIMARY KEY (VolunteerId, SkillId)
);

CREATE TABLE VolunteerAvailability (
    AvailabilityId INT IDENTITY PRIMARY KEY,
    VolunteerId INT NOT NULL REFERENCES Volunteers(VolunteerId),
    StartsAt    DATETIME2 NOT NULL,
    EndsAt      DATETIME2 NOT NULL,
    CONSTRAINT CK_Avail_Range CHECK (EndsAt > StartsAt)
);

CREATE TABLE VolunteerTasks (
    TaskId      INT IDENTITY PRIMARY KEY,
    CrisisId    INT NOT NULL REFERENCES CrisisEvents(CrisisId),
    RequestId   BIGINT NULL REFERENCES AssistanceRequests(RequestId),
    ShelterId   INT NULL REFERENCES Shelters(ShelterId),
    Title       NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    RequiredSkillId INT NULL REFERENCES Skills(SkillId),
    VolunteersNeeded INT NOT NULL DEFAULT 1 CHECK (VolunteersNeeded > 0),
    StartsAt    DATETIME2 NULL,
    Status      NVARCHAR(20) NOT NULL DEFAULT 'Open' CHECK (Status IN ('Open','Filled','InProgress','Completed','Cancelled')),
    CreatedBy   INT NOT NULL REFERENCES Users(UserId)
);

CREATE TABLE TaskAssignments (
    AssignmentId INT IDENTITY PRIMARY KEY,
    TaskId      INT NOT NULL REFERENCES VolunteerTasks(TaskId),
    VolunteerId INT NOT NULL REFERENCES Volunteers(VolunteerId),
    RecommendationId BIGINT NULL REFERENCES AiRecommendations(RecommendationId),
    Status      NVARCHAR(20) NOT NULL DEFAULT 'Offered' CHECK (Status IN ('Offered','Accepted','Declined','Completed','NoShow')),
    HoursLogged DECIMAL(5,2) NULL,
    RespondedAt DATETIME2 NULL,
    CompletedAt DATETIME2 NULL,
    CONSTRAINT UQ_Task_Volunteer UNIQUE (TaskId, VolunteerId)
);

/* ---------- 8. DONORS & DONATIONS ---------- */
CREATE TABLE Donors (
    DonorId  INT IDENTITY PRIMARY KEY,
    UserId   INT NULL REFERENCES Users(UserId),
    Name     NVARCHAR(200) NOT NULL,
    Type     NVARCHAR(20) NOT NULL DEFAULT 'Individual' CHECK (Type IN ('Individual','Corporate','Foundation','Government')),
    Email    NVARCHAR(256) NULL,
    IsAnonymous BIT NOT NULL DEFAULT 0
);

CREATE TABLE Campaigns (
    CampaignId INT IDENTITY PRIMARY KEY,
    CrisisId   INT NOT NULL REFERENCES CrisisEvents(CrisisId),
    OrganisationId INT NOT NULL REFERENCES Organisations(OrganisationId),
    Name       NVARCHAR(200) NOT NULL,
    TargetAmount DECIMAL(14,2) NOT NULL CHECK (TargetAmount > 0),
    StartsOn   DATE NOT NULL,
    EndsOn     DATE NULL,
    Status     NVARCHAR(20) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Draft','Active','Closed'))
);

CREATE TABLE Donations (
    DonationId BIGINT IDENTITY PRIMARY KEY,
    DonorId    INT NOT NULL REFERENCES Donors(DonorId),
    CampaignId INT NOT NULL REFERENCES Campaigns(CampaignId),
    Type       NVARCHAR(10) NOT NULL CHECK (Type IN ('Monetary','Material')),
    Amount     DECIMAL(14,2) NULL,
    Currency   CHAR(3) NULL,
    ResourceTypeId INT NULL REFERENCES ResourceTypes(ResourceTypeId),
    Quantity   DECIMAL(12,2) NULL,
    PaymentRef NVARCHAR(100) NULL UNIQUE,            -- sandbox gateway reference (idempotency)
    IsFlagged  BIT NOT NULL DEFAULT 0,
    FlagReason NVARCHAR(500) NULL,
    ReceivedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Donation_Shape CHECK (
        (Type = 'Monetary' AND Amount > 0 AND Currency IS NOT NULL) OR
        (Type = 'Material' AND ResourceTypeId IS NOT NULL AND Quantity > 0))
);

CREATE TABLE DonationAllocations (   -- expenditure tracking
    DonationAllocationId BIGINT IDENTITY PRIMARY KEY,
    DonationId  BIGINT NOT NULL REFERENCES Donations(DonationId),
    Purpose     NVARCHAR(200) NOT NULL,
    AmountUsed  DECIMAL(14,2) NULL CHECK (AmountUsed > 0),
    InventoryId INT NULL REFERENCES ResourceInventories(InventoryId),
    ApprovedBy  INT NOT NULL REFERENCES Users(UserId),
    AllocatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

/* ---------- 9. NOTIFICATIONS & ASYNC PROCESSING ---------- */
CREATE TABLE Notifications (
    NotificationId BIGINT IDENTITY PRIMARY KEY,
    UserId     INT NOT NULL REFERENCES Users(UserId),
    RequestId  BIGINT NULL REFERENCES AssistanceRequests(RequestId),
    Channel    NVARCHAR(10) NOT NULL CHECK (Channel IN ('Email','Sms','Push','InApp')),
    TemplateCode NVARCHAR(60) NOT NULL,
    Language   NVARCHAR(10) NOT NULL DEFAULT 'en',
    Subject    NVARCHAR(300) NULL,
    Body       NVARCHAR(MAX) NOT NULL,
    IdempotencyKey NVARCHAR(150) NOT NULL,           -- eventId + user + channel => no duplicates
    Status     NVARCHAR(15) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','Sent','Failed','DeadLetter')),
    RetryCount INT NOT NULL DEFAULT 0,
    NextRetryAt DATETIME2 NULL,
    CreatedAt  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    SentAt     DATETIME2 NULL,
    CONSTRAINT UQ_Notification_Idem UNIQUE (IdempotencyKey)
);

CREATE TABLE NotificationAttempts (
    AttemptId BIGINT IDENTITY PRIMARY KEY,
    NotificationId BIGINT NOT NULL REFERENCES Notifications(NotificationId),
    Provider  NVARCHAR(60) NOT NULL,
    Success   BIT NOT NULL,
    ErrorMessage NVARCHAR(1000) NULL,
    AttemptedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE OutboxMessages (        -- transactional outbox => event-driven workflow
    MessageId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    EventType  NVARCHAR(100) NOT NULL,               -- RequestSubmitted, AiRecommendationDecided...
    Payload    NVARCHAR(MAX) NOT NULL,
    CreatedAt  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ProcessedAt DATETIME2 NULL,
    RetryCount INT NOT NULL DEFAULT 0,
    LastError  NVARCHAR(1000) NULL
);

CREATE TABLE ProcessedMessages (     -- consumer-side deduplication
    MessageId   UNIQUEIDENTIFIER NOT NULL,
    Consumer    NVARCHAR(100) NOT NULL,
    ProcessedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    PRIMARY KEY (MessageId, Consumer)
);

/* ---------- 10. AUDIT, KPIs, MAPPING ---------- */
CREATE TABLE AuditEvents (
    AuditId    BIGINT IDENTITY PRIMARY KEY,
    OccurredAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId     INT NULL REFERENCES Users(UserId),
    Action     NVARCHAR(100) NOT NULL,               -- e.g. AI_DECISION, LOGIN, ALLOCATE_RESOURCE
    EntityType NVARCHAR(60) NOT NULL,
    EntityId   NVARCHAR(60) NOT NULL,
    OldValue   NVARCHAR(MAX) NULL,
    NewValue   NVARCHAR(MAX) NULL,
    IpAddress  NVARCHAR(45) NULL,
    CorrelationId UNIQUEIDENTIFIER NULL
);

CREATE TABLE KpiSnapshots (
    SnapshotId BIGINT IDENTITY PRIMARY KEY,
    CrisisId   INT NULL REFERENCES CrisisEvents(CrisisId),
    CapturedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UnresolvedCases INT NOT NULL,
    AvgResponseMinutes DECIMAL(10,2) NULL,
    ResourceUtilisationPct DECIMAL(5,2) NULL,
    ShelterOccupancyPct DECIMAL(5,2) NULL,
    AiOverrideRatePct DECIMAL(5,2) NULL
);

CREATE TABLE ServiceAreas (          -- coverage analysis for shelters / centres
    ServiceAreaId INT IDENTITY PRIMARY KEY,
    ShelterId  INT NOT NULL REFERENCES Shelters(ShelterId),
    RadiusKm   DECIMAL(8,2) NOT NULL,
    Notes      NVARCHAR(300) NULL
);

CREATE TABLE GeocodeCache (          -- reduces calls to external mapping provider
    CacheId   BIGINT IDENTITY PRIMARY KEY,
    QueryHash CHAR(64) NOT NULL UNIQUE,
    Latitude  DECIMAL(9,6) NOT NULL,
    Longitude DECIMAL(9,6) NOT NULL,
    FormattedAddress NVARCHAR(400) NULL,
    CachedAt  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Feedback (
    FeedbackId BIGINT IDENTITY PRIMARY KEY,
    RequestId  BIGINT NOT NULL UNIQUE REFERENCES AssistanceRequests(RequestId),
    Rating     TINYINT NOT NULL CHECK (Rating BETWEEN 1 AND 5),
    Comment    NVARCHAR(1000) NULL,
    CreatedAt  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE PublicAlerts (          -- verified public alerts / emergency announcements
    AlertId     INT IDENTITY PRIMARY KEY,
    CrisisId    INT NULL REFERENCES CrisisEvents(CrisisId),
    Title       NVARCHAR(200) NOT NULL,
    Body        NVARCHAR(2000) NOT NULL,
    Severity    NVARCHAR(10) NOT NULL DEFAULT 'Info' CHECK (Severity IN ('Info','Warning','Critical')),
    Language    NVARCHAR(10) NOT NULL DEFAULT 'en',
    PublishedBy INT NOT NULL REFERENCES Users(UserId),
    PublishedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ExpiresAt   DATETIME2 NULL,
    IsActive    BIT NOT NULL DEFAULT 1
);
GO

/* ---------- 11. INDEXES ---------- */
CREATE INDEX IX_Requests_Status_Priority ON AssistanceRequests (CrisisId, Status, FinalPriority) INCLUDE (CreatedAt, Category);
CREATE INDEX IX_Requests_Geo            ON AssistanceRequests (Latitude, Longitude);
CREATE INDEX IX_RequestHistory_Request  ON RequestHistory (RequestId, ChangedAt);
CREATE INDEX IX_AiRec_Request           ON AiRecommendations (RequestId, Type, Status);
CREATE INDEX IX_Decisions_Rec           ON HumanDecisions (RecommendationId);
CREATE INDEX IX_ShelterAlloc_Shelter    ON ShelterAllocations (ShelterId, Status);
CREATE INDEX IX_ResAlloc_Inventory      ON ResourceAllocations (InventoryId, Status);
CREATE INDEX IX_Notif_Pending           ON Notifications (Status, NextRetryAt) WHERE Status IN ('Pending','Failed');
CREATE INDEX IX_Outbox_Unprocessed      ON OutboxMessages (ProcessedAt, CreatedAt) WHERE ProcessedAt IS NULL;
CREATE INDEX IX_Audit_Entity            ON AuditEvents (EntityType, EntityId, OccurredAt);
CREATE INDEX IX_Audit_User              ON AuditEvents (UserId, OccurredAt);
GO

/* ---------- 12. SEED DATA ---------- */
INSERT INTO Roles (Name) VALUES
 ('Citizen'),('EmergencyCoordinator'),('CaseManager'),('LogisticsManager'),
 ('ShelterAdmin'),('VolunteerCoordinator'),('DonorOfficer'),('PartnerOfficer'),('SystemAdmin');

INSERT INTO Permissions (Code, Description) VALUES
 ('REQUEST_CREATE','Submit assistance request'),
 ('REQUEST_REVIEW','Review and prioritise requests'),
 ('AI_DECIDE','Approve / modify / reject / defer AI recommendations'),
 ('AI_DECIDE_CRITICAL','Second approval for critical decisions'),
 ('RESOURCE_ALLOCATE','Allocate resources'),
 ('SHELTER_MANAGE','Manage shelters and allocations'),
 ('VOLUNTEER_MANAGE','Manage volunteers and tasks'),
 ('DONATION_MANAGE','Manage donations and campaigns'),
 ('AUDIT_VIEW','View audit log'),
 ('USER_ADMIN','Manage users and roles');

-- Example mapping (extend as needed)
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId FROM Roles r JOIN Permissions p ON
   (r.Name='Citizen'              AND p.Code IN ('REQUEST_CREATE'))
OR (r.Name='EmergencyCoordinator' AND p.Code IN ('REQUEST_REVIEW','AI_DECIDE','AI_DECIDE_CRITICAL','RESOURCE_ALLOCATE','AUDIT_VIEW'))
OR (r.Name='CaseManager'          AND p.Code IN ('REQUEST_REVIEW','AI_DECIDE'))
OR (r.Name='LogisticsManager'     AND p.Code IN ('RESOURCE_ALLOCATE'))
OR (r.Name='ShelterAdmin'         AND p.Code IN ('SHELTER_MANAGE'))
OR (r.Name='VolunteerCoordinator' AND p.Code IN ('VOLUNTEER_MANAGE'))
OR (r.Name='DonorOfficer'         AND p.Code IN ('DONATION_MANAGE'))
OR (r.Name='SystemAdmin'          AND p.Code IN ('USER_ADMIN','AUDIT_VIEW'));

INSERT INTO ResourceTypes (Name, Category, Unit, IsPerishable) VALUES
 ('Dry ration pack','Food','pack',0),('Drinking water','Water','litre',0),
 ('Medical kit','Medicine','kit',0),('Ambulance','Vehicle','unit',0),('Truck','Vehicle','unit',0);

INSERT INTO Skills (Name) VALUES ('First aid'),('Driving'),('Translation'),('Cooking'),('Counselling'),('Logistics');
GO

-- Phone-number login: one account per phone number (run once on the existing database).
SET QUOTED_IDENTIFIER ON;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Users_Phone' AND object_id = OBJECT_ID('Users'))
    CREATE UNIQUE INDEX UX_Users_Phone ON Users(Phone) WHERE Phone IS NOT NULL;
