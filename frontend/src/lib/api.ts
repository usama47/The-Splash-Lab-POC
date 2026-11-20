const API_BASE = process.env.NEXT_PUBLIC_API_BASE || "http://localhost:5080";

export function errMessage(e: unknown): string {
  if (e instanceof Error) return e.message;
  return String(e);
}

export class ApiError extends Error {
  constructor(message: string, public status: number, public data: unknown) {
    super(message);
  }
}

const USER_KEY = "splashlab.demoUserId";

export function getDemoUserId(): string {
  if (typeof window === "undefined") return "22222222-2222-2222-2222-222222222222";
  return localStorage.getItem(USER_KEY) || "22222222-2222-2222-2222-222222222222";
}

export function setDemoUserId(id: string) {
  localStorage.setItem(USER_KEY, id);
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers || {});
  if (!headers.has("Content-Type") && init.body && !(init.body instanceof FormData)) {
    headers.set("Content-Type", "application/json");
  }
  headers.set("X-Demo-User-Id", getDemoUserId());

  const res = await fetch(`${API_BASE}${path}`, { ...init, headers, cache: "no-store" });
  if (!res.ok) {
    let message = res.statusText;
    let data: unknown = undefined;
    try {
      data = await res.json();
      const body = data as { error?: string; message?: string; title?: string };
      message = body.error || body.message || body.title || JSON.stringify(data);
    } catch {
      /* ignore */
    }
    throw new ApiError(message, res.status, data);
  }
  if (res.status === 204) return undefined as T;
  return res.json();
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) }),
  put: <T>(path: string, body: unknown) =>
    request<T>(path, { method: "PUT", body: JSON.stringify(body) }),
  upload: <T>(path: string, form: FormData) => request<T>(path, { method: "POST", body: form }),
};

export type Dashboard = {
  meetingsProcessed: number;
  draftsAwaitingApproval: number;
  approvedDecisions: number;
  openCommitments: number;
  overdueCommitments: number;
  processesMapped: number;
  workflowProgress: { step: string; count: number }[];
};

export type MeetingSummary = {
  id: string;
  title: string;
  meetingDate: string;
  department: string;
  organizer: string;
  source: string;
  scenarioKey: string;
  workflowStatus: string;
};

export type DraftItem = {
  id: string;
  statement?: string;
  context?: string;
  action?: string;
  title?: string;
  description?: string;
  suggestedOwner?: string | null;
  suggestedDueDate?: string | null;
  sourceEvidence: string;
  reviewStatus: string;
  needsReview: boolean;
  needsReviewReason?: string;
};

export type PublishedTask = {
  commitmentId: string;
  action: string;
  owner: string;
  dueDate: string | null;
  taskId: string | null;
  externalTaskId: string;
  status: string;
  isNew: boolean;
  failureReason: string | null;
};

export type PublishResult = {
  succeeded: boolean;
  message: string;
  meetingId: string;
  decisionsPublished: number;
  commitmentsPublished: number;
  tasksCreated: number;
  tasksNewlyCreated: number;
  tasksAlreadyExisting: number;
  tasksFailed: number;
  tasks: PublishedTask[];
  errors: string[];
};

export type MeetingDetail = MeetingSummary & {
  transcript: string;
  draft: {
    id: string;
    version: string;
    summary: string;
    discussionPoints: string[];
    providerName: string;
    isPublished: boolean;
    decisions: DraftItem[];
    commitments: DraftItem[];
    issues: DraftItem[];
  } | null;
};
