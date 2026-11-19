"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { api, getDemoUserId, setDemoUserId } from "@/lib/api";
import styles from "./AppShell.module.css";

const NAV = [
  { href: "/", label: "Dashboard" },
  { href: "/meetings", label: "Meetings" },
  { href: "/review", label: "Review Queue" },
  { href: "/commitments", label: "Commitments and Tasks" },
  { href: "/decisions", label: "Decisions" },
  { href: "/issues", label: "Issues" },
  { href: "/knowledge", label: "Knowledge" },
  { href: "/processes", label: "Process Register" },
  { href: "/audit", label: "Audit Log" },
  { href: "/integrations", label: "Integration Settings" },
];

type UserInfo = {
  current: { userId: string; displayName: string; email: string; role: string };
  demoModeNotice: string;
  users: { id: string; displayName: string; email: string; department: string; role: string }[];
};

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const [users, setUsers] = useState<UserInfo | null>(null);
  const [userId, setUserId] = useState(getDemoUserId());

  useEffect(() => {
    api.get<UserInfo>("/api/users").then(setUsers).catch(() => setUsers(null));
  }, [userId]);

  function onSwitch(id: string) {
    setDemoUserId(id);
    setUserId(id);
    window.location.reload();
  }

  return (
    <div className={styles.shell}>
      <aside className={styles.sidebar}>
        <div className={styles.brand}>
          <div className={styles.brandName}>The Splash Lab</div>
          <div className={styles.brandSub}>AI & Business Systems Foundation</div>
        </div>
        <nav className={styles.nav}>
          {NAV.map((item) => {
            const active = item.href === "/" ? pathname === "/" : pathname.startsWith(item.href);
            return (
              <Link key={item.href} href={item.href} className={active ? styles.active : undefined}>
                {item.label}
              </Link>
            );
          })}
        </nav>
        <div className={styles.demoBox}>
          <div className={styles.demoTitle}>Local demo identity</div>
          <p className={styles.demoNote}>
            Not Microsoft Entra. Switch roles for local RBAC demonstration only.
          </p>
          <select value={userId} onChange={(e) => onSwitch(e.target.value)} aria-label="Demo user">
            {(users?.users || [
              { id: "11111111-1111-1111-1111-111111111111", displayName: "Alex Morgan", role: "Employee" },
              { id: "22222222-2222-2222-2222-222222222222", displayName: "Jordan Lee", role: "Reviewer" },
              { id: "33333333-3333-3333-3333-333333333333", displayName: "Sam Patel", role: "Administrator" },
            ]).map((u) => (
              <option key={u.id} value={u.id}>
                {u.displayName} ({u.role})
              </option>
            ))}
          </select>
          {users?.current && (
            <div className={styles.current}>
              Signed in as <strong>{users.current.displayName}</strong> · {users.current.role}
            </div>
          )}
        </div>
      </aside>
      <main className={styles.main}>
        <header className={styles.top}>
          <div>
            <div className={styles.mode}>MODE A — Local demo · Simulated integrations</div>
          </div>
        </header>
        <div className={styles.content}>{children}</div>
      </main>
    </div>
  );
}
