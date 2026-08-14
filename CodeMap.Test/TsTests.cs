using System.Linq;
using System.IO;
using Microsoft.VisualStudio.Text.Tagging;
using Xunit;

namespace CodeMap.Test
{
    public class TsTests
    {
        [Fact]
        public void MapperDebug_UserCard()
        {
            var code = @"const UserCard: React.FC<UserCardProps> = ({ user, onSelect }: UserCardProps) => {
  return (
    <article style={cardStyle} onClick={() => onSelect?.(user)}>
      <h3 style={{ margin: 0 }}>{user.name}</h3>
      <p style={{ margin: '4px 0 0 0', fontSize: 12, color: '#444' }}>{user.email ?? 'No email'}</p>
      <small style={{ color: '#666' }}>Joined: {formatDate(user.joined)}</small>
    </article>
  );
};";

            var codeLines = code.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
            var map = TypeScriptMapper.Generate(codeLines, true);

            var items = map.Select(x => x).Concat(map.SelectMany(x => x.Children)).ToArray();

            var outDir = "TestArtifacts";
            System.IO.Directory.CreateDirectory(outDir);
            var outPath = System.IO.Path.Combine(outDir, "UserCardMap.txt");
            using (var w = System.IO.File.CreateText(outPath))
            {
                foreach (var it in items)
                {
                    w.WriteLine($"Line:{it.Line} Type:{it.MemberType} Parent:{it.ParentPath} Name:{it.Name} Content:{it.Content}");
                }
            }

            Assert.NotEmpty(items);
        }

        [Fact]
        public void Mapper_Interface_WithSixProperties()
        {
            var code = @"interface User {
  ids: number;
  name: string;
  email?: string;
  joined: ISODate; // ISO date
  role?: UserRole;
  status?: UserStatus;
}";

            var codeLines = code.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
            var map = TypeScriptMapper.Generate(codeLines, true);
            var members = map.StructureTs().ToArray();

            // find the interface
            var iface = members.FirstOrDefault(m => m.MemberType == MemberType.Interface && m.Name == "User");
            Assert.NotNull(iface);

            // children may be populated on the MemberInfo.Children after StructureTs
            var children = iface.Children.ToArray();
            Assert.Equal(6, children.Length);
        }

        [Fact]
        public void UpdatedMapper_PR37()
        {
            var code = """
            export function actual_output(element: string, index: any, array: any) {
                // ignore mono test output that comes from older releases(s)  (known Mono issue)
                return (
                    !element.startsWith('failed to get 100ns ticks') &&
                    !element.startsWith('Mono pdb to mdb debug symbol store converter') &&
                    !element.startsWith('Usage: pdb2mdb assembly'));
            }

            export type BuiltInCommands = 'workbench.action.closeActiveEditor' | 'workbench.action.nextEditor';
            export const BuiltInCommands = {
                CloseActiveEditor: 'workbench.action.closeActiveEditor' as BuiltInCommands,
                NextEditor: 'workbench.action.nextEditor' as BuiltInCommands,
                Open: 'vscode.open' as BuiltInCommands,
                SetContext: 'setContext' as BuiltInCommands
            };
            """;

            var map = TypeScriptMapper.Generate(code.GetLines(), true);

            var members = map.StructureTs();

            // no exception on duplicated `BuiltInCommands` keys
        }

        [Fact]
        public void SamplePage_Mapping_CoversExpectedEntries()
        {
            // Use an inline sample (taken from sample files/SamplePage.tsx) so tests don't access the file system
            var code = """
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
  await new Promise((r) => setTimeout(r, 350));
  return [...DEFAULT_USERS, { id: 3, name: 'Charlie', email: 'charlie@host.local', joined: '2024-05-01', role: 'guest', status: 'active' }];
}

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

  return null;
};

const cardStyle: React.CSSProperties = {
  padding: 12,
  border: '1px solid #ddd',
  borderRadius: 6,
  cursor: 'pointer',
  background: '#fafafa',
};

export default SamplePage;
""";

            var codeLines = code.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
            var map = TypeScriptMapper.Generate(codeLines, true);
            var roots = map.StructureTs().ToArray();

            // basic type/enum entries
            Assert.Contains(roots, r => r.MemberType == MemberType.Type && r.Name == "UserRole");
            Assert.Contains(roots, r => r.MemberType == MemberType.Type && r.Name == "UserStatus");
            Assert.Contains(roots, r => r.MemberType == MemberType.Type && r.Name == "ISODate");
            Assert.Contains(roots, r => r.MemberType == MemberType.Type && r.Name == "Color");

            // interfaces
            Assert.Contains(roots, r => r.MemberType == MemberType.Interface && r.Name == "User");
            Assert.Contains(roots, r => r.MemberType == MemberType.Interface && r.Name == "SamplePageProps");
            Assert.Contains(roots, r => r.MemberType == MemberType.Interface && r.Name == "UserCardProps");
            Assert.Contains(roots, r => r.MemberType == MemberType.Interface && r.Name == "ToggleButtonProps");
            Assert.Contains(roots, r => r.MemberType == MemberType.Interface && r.Name == "CounterClassProps");

            // check User properties
            var userIf = roots.First(r => r.MemberType == MemberType.Interface && r.Name == "User");
            var userChildren = userIf.Children.Select(c => c.Content).OrderBy(x => x).ToArray();
            Assert.Equal(6, userChildren.Length);
            Assert.Contains("ids", userChildren);
            Assert.Contains("name", userChildren);
            Assert.Contains("email", userChildren);
            Assert.Contains("joined", userChildren);
            Assert.Contains("role", userChildren);
            Assert.Contains("status", userChildren);

            // SamplePageProps should have showHeader property and function method
            var sp = roots.First(r => r.MemberType == MemberType.Interface && r.Name == "SamplePageProps");
            Assert.Contains(sp.Children, c => c.MemberType == MemberType.Property && c.Content == "showHeader");
            Assert.Contains(sp.Children, c => c.MemberType == MemberType.Method && c.MethodParameters == "arg: string");

            // UserCardProps and ToggleButtonProps method signatures
            var ucp = roots.First(r => r.MemberType == MemberType.Interface && r.Name == "UserCardProps");
            Assert.Contains(ucp.Children, c => c.MemberType == MemberType.Property && c.Content == "user");
            Assert.Contains(ucp.Children, c => c.MemberType == MemberType.Method && c.MethodParameters.Contains("User"));

            var tbp = roots.First(r => r.MemberType == MemberType.Interface && r.Name == "ToggleButtonProps");
            Assert.Contains(tbp.Children, c => c.MemberType == MemberType.Method && c.MethodParameters.Contains("boolean"));

            // consts and default users
            Assert.Contains(roots, r => r.MemberType == MemberType.Property && r.Name == "PAGE_TITLE");
            Assert.Contains(roots, r => r.MemberType == MemberType.Property && r.Name == "DEFAULT_USERS");

            // top-level functions
            Assert.Contains(roots, r => r.MemberType == MemberType.Method && r.Name == "formatDate");
            Assert.Contains(roots, r => r.MemberType == MemberType.Method && r.Name == "fetchUsersMock");
            Assert.Contains(roots, r => r.MemberType == MemberType.Method && r.Name == "useIsMounted");

            // class and its render method
            Assert.Contains(roots, r => r.MemberType == MemberType.Class && r.Name == "CounterClass");
            var cc = roots.First(r => r.MemberType == MemberType.Class && r.Name == "CounterClass");
            Assert.Contains(cc.Children, c => c.MemberType == MemberType.Method && c.Content.StartsWith("render"));

            // components/constants present
            Assert.Contains(roots, r => r.MemberType == MemberType.Property && r.Name == "UserCard");
            Assert.Contains(roots, r => r.MemberType == MemberType.Property && r.Name == "MemoizedUserCard");
            Assert.Contains(roots, r => r.MemberType == MemberType.Property && r.Name == "ToggleButton");
            Assert.Contains(roots, r => r.MemberType == MemberType.Property && r.Name == "SamplePage");
        }
    }
}
