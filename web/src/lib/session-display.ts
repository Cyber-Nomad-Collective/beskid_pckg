import type { Session } from "./pckg-api";

export function sessionDisplayName(session: Session): string {
	return session.displayName?.trim() || session.subject;
}
