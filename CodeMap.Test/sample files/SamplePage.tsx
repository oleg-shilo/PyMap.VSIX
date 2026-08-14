import React, { useEffect, useState, useRef, memo } from 'react';

// --- Enums & Types (local) ---
type UserRole = 'admin' | 'member' | 'guest';

type UserStatus = 'active' | 'inactive' | 'banned';

type ISODate = string;

enum Color {
    red = 'red',
    green = 'green',
    blue = 'blue',
}

interface User {
  ids: number;
  name: string;
  email?: string;
  joined: ISODate; // ISO date
  role?: UserRole;
  status?: UserStatus;
}

interface SamplePageProps {
    showHeader?: boolean;
    function?: (arg: string) => void;
}

interface UserCardProps {
  user: User;
  onSelect?: (u: User) => void;
}

interface ToggleButtonProps {
  onToggle?: (v: boolean) => void;
}

interface CounterClassProps {
  start?: number;
}

// --- Constants ---
const PAGE_TITLE = 'Sample TSX Page';

const DEFAULT_USERS: User[] = [
  { id: 1, name: 'Alice', email: 'alice@example.com', joined: '2022-01-10', role: 'admin', status: 'active' },
  { id: 2, name: 'Bob', joined: '2023-03-22', role: 'member', status: 'inactive' },
];

// --- Utility functions ---
function formatDate(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleDateString();
  } catch {
    return iso;
  }
}

async function fetchUsersMock(): Promise<User[]> {
  // Simulate network latency
  await new Promise((r) => setTimeout(r, 350));
  return [...DEFAULT_USERS, { id: 3, name: 'Charlie', email: 'charlie@host.local', joined: '2024-05-01', role: 'guest', status: 'active' }];
}

// --- Custom Hook ---
function useIsMounted() {
  const isMounted = useRef(false);
  useEffect(() => {
    isMounted.current = true;
    return () => {
      isMounted.current = false;
    };
  }, []);
  return isMounted;
}

// --- Functional Components ---
const UserCard: React.FC<UserCardProps> = ({ user, onSelect }: UserCardProps) => {
  return (
    <article style={cardStyle} onClick={() => onSelect?.(user)}>
      <h3 style={{ margin: 0 }}>{user.name}</h3>
      <p style={{ margin: '4px 0 0 0', fontSize: 12, color: '#444' }}>{user.email ?? 'No email'}</p>
      <small style={{ color: '#666' }}>Joined: {formatDate(user.joined)}</small>
    </article>
  );
};

const MemoizedUserCard = memo(UserCard);

const ToggleButton: React.FC<ToggleButtonProps> = ({ onToggle }: ToggleButtonProps) => {
  const [on, setOn] = useState<boolean>(false);
  return (
    <button
      onClick={() => {
        setOn((s) => {
          const next = !s;
          onToggle?.(next);
          return next;
        });
      }}
    >
      {on ? 'On' : 'Off'}
    </button>
  );
};

// --- Class Component ---
class CounterClass extends React.Component<CounterClassProps> {
  state = { count: this.props.start ?? 0 };

  increment = () => this.setState((s: any) => ({ count: s.count + 1 }));

  render() {
    return (
      <div style={{ marginTop: 8 }}>
        <strong>Class Counter:</strong>
        <div>{this.state.count}</div>
        <button onClick={this.increment}>+1</button>
      </div>
    );
  }
}

// --- Main Page Component ---
const SamplePage: React.FC<SamplePageProps> = ({ showHeader = true }) => {
  const [users, setUsers] = useState<User[]>(DEFAULT_USERS);
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<User | null>(null);
  const isMounted = useIsMounted();

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      const data = await fetchUsersMock();
      if (!cancelled && isMounted.current) setUsers(data);
      setLoading(false);
    })();
    return () => {
      cancelled = true;
    };
  }, [isMounted]);

  return (
    <main style={{ fontFamily: 'Segoe UI, Roboto, system-ui', padding: 16 }}>
      {showHeader && (
        <header style={{ marginBottom: 12 }}>
          <h1 style={{ margin: 0 }}>{PAGE_TITLE}</h1>
          <p style={{ margin: '6px 0 0 0', color: '#666' }}>A small page demonstrating TSX patterns.</p>
        </header>
      )}

      <section>
        <ToggleButton onToggle={(v: boolean) => console.log('toggled', v)} />
        <CounterClass start={5} />
      </section>

      <section style={{ marginTop: 16 }}>
        <h2>Users</h2>
        {loading ? (
          <div>Loading users...</div>
        ) : (
          <div style={{ display: 'grid', gap: 8 }}>
            {users.map((u: User) => (
              <MemoizedUserCard key={u.id} user={u} onSelect={(x: User) => setSelected(x)} />
            ))}
          </div>
        )}
      </section>

      <aside style={{ marginTop: 16 }}>
        <h3>Selected</h3>
        {selected ? (
          <div>
            <div>{selected.name}</div>
            <div style={{ fontSize: 12, color: '#555' }}>{selected.email ?? '—'}</div>
          </div>
        ) : (
          <div>None selected</div>
        )}
      </aside>
    </main>
  );
};

// --- Local styles ---
const cardStyle: React.CSSProperties = {
  padding: 12,
  border: '1px solid #ddd',
  borderRadius: 6,
  cursor: 'pointer',
  background: '#fafafa',
};

export default SamplePage;
