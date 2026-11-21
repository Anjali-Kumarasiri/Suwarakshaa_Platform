namespace Domain.Entities;

public enum RequestStatus { Submitted, UnderReview, Verified, Escalated, Assigned, InProgress, Fulfilled, Rejected, Merged, Closed }
public enum RecommendationStatus { PendingReview, Approved, Modified, Rejected, Deferred, Expired }
public enum DecisionAction { Approve, Modify, Reject, Defer }
