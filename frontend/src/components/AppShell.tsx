"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { api, getDemoUserId, setDemoUserId } from "@/lib/api";
import { Toaster } from "./Toaster";
import styles from "./AppShell.module.css";

const NAV = [
  { href: "/", label: "Dashboard", icon: "◈", section: "Overview" },
  { href: "/meetings", label: "Meetings", icon: "◎", section: "Workflow" },
  { href: "/review", label: "Review Queue", icon: "✓", section: "Workflow" },
  { href: "/commitments", label: "Commitments and Tasks", icon: "▣", section: "Registers" },
  { href: "/decisions", label: "Decisions", icon: "◆", section: "Registers" },
  { href: "/issues", label: "Issues", icon: "⚠", section: "Registers" },
  { href: "/knowledge", label: "Knowledge", icon: "☰", section: "Registers" },
  { href: "/processes", label: "Process Register", icon: "▦", section: "Registers" },
  { href: "/audit", label: "Audit Log", icon: "◉", section: "Governance" },
  { href: "/integrations", label: "Integration Settings", icon: "⬡", section: "Governance" },
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

  let lastSection = "";

  return (
    <div className={styles.shell}>
      <aside className={styles.sidebar}>
        <div className={styles.brand}>
          <div className={styles.brandMark}>SL</div>
          <div className={styles.brandName}>The Splash Lab</div>
          <div className={styles.brandSub}>AI &amp; Business Systems Foundation</div>
        </div>
        <nav className={styles.nav}>
          {NAV.map((item) => {
            const showSection = item.section !== lastSection;
            lastSection = item.section;
            const active = item.href === "/" ? pathname === "/" : pathname.startsWith(item.href);
            return (
              <div key={item.href}>
                {showSection && <div className={styles.navSection}>{item.section}</div>}
                <Link href={item.href} className={active ? styles.active : undefined}>
                  <span className={styles.navIcon}>{item.icon}</span>
                  {item.label}
                </Link>
              </div>
            );
          })}
        </nav>
        <div className={styles.demoBox}>
          <div className={styles.demoTitle}>Local demo identity</div>
          <p className={styles.demoNote}>
            Not Microsoft Entra. Switch roles only for this local RBAC demonstration.
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
              Signed in as <strong>{users.current.displayName}</strong>
              <br />
              {users.current.role} · Operations pilot
            </div>
          )}
        </div>
      </aside>
      <main className={styles.main}>
        <header className={styles.top}>
          <div className={styles.mode}>
            <span className={styles.modeDot} />
            MODE A — Local demo · Simulated integrations
          </div>
          <div className={styles.topMeta}>Pilot department: Operations</div>
        </header>
        <div className={`${styles.content} page-enter`}>{children}</div>
      </main>
      <Toaster />
    </div>
  );
}
