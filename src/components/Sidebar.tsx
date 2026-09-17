import React from 'react';
import { 
  LayoutDashboard, 
  FileText, 
  Layers, 
  Calendar, 
  Users, 
  GraduationCap, 
  Settings, 
  LogOut, 
  Flame,
  BookOpen,
  X
} from 'lucide-react';
import { NavigationPage, UserProfile } from '../types';

interface SidebarProps {
  currentPage: NavigationPage;
  onNavigate: (page: NavigationPage) => void;
  user?: UserProfile | null;
  onLogoutClick?: () => void;
  onLogout?: () => void;
  isMobileOpen?: boolean;
  onCloseMobile?: () => void;
  unreadCount?: number;
  stats?: {
    notesCount: number;
    cardsCount: number;
    eventsCount: number;
    groupsCount: number;
  };
}

export const Sidebar: React.FC<SidebarProps> = ({
  currentPage,
  onNavigate,
  user,
  onLogoutClick,
  onLogout,
  isMobileOpen = false,
  onCloseMobile = () => {},
  stats
}) => {
  const streakDays = user?.studyStreakDays ?? 0;

  const handleLogout = () => {
    if (onLogoutClick) {
      onLogoutClick();
    } else if (onLogout) {
      onLogout();
    }
  };

  const navItems = [
    {
      id: 'dashboard' as NavigationPage,
      label: 'Tổng quan',
      icon: LayoutDashboard,
      badge: null
    },
    {
      id: 'notes' as NavigationPage,
      label: 'Ghi chú & Tài liệu',
      icon: FileText,
      badge: stats ? `${stats.notesCount}` : null
    },
    {
      id: 'flashcards' as NavigationPage,
      label: 'Thẻ ghi nhớ (Flashcard)',
      icon: Layers,
      badge: stats ? `${stats.cardsCount}` : null
    },
    {
      id: 'calendar' as NavigationPage,
      label: 'Lịch học cá nhân',
      icon: Calendar,
      badge: stats ? `${stats.eventsCount}` : null
    },
    {
      id: 'groups' as NavigationPage,
      label: 'Nhóm học tập',
      icon: Users,
      badge: stats ? `${stats.groupsCount}` : null
    },
    {
      id: 'grades' as NavigationPage,
      label: 'Điểm số & Mục tiêu',
      icon: GraduationCap,
      badge: '8.4'
    },
    {
      id: 'settings' as NavigationPage,
      label: 'Cài đặt hệ thống',
      icon: Settings,
      badge: null
    },
  ];

  const handleItemClick = (page: NavigationPage) => {
    onNavigate(page);
    onCloseMobile();
  };

  return (
    <>
      {/* Mobile backdrop overlay */}
      {isMobileOpen && (
        <div
          id="sidebar-mobile-backdrop"
          className="fixed inset-0 z-40 bg-slate-900/50 backdrop-blur-xs lg:hidden transition-opacity"
          onClick={onCloseMobile}
        />
      )}

      {/* Main Sidebar Container */}
      <aside
        id="app-sidebar"
        className={`fixed top-0 left-0 bottom-0 z-40 w-64 bg-white border-r border-slate-200 flex flex-col transition-transform duration-200 ease-in-out lg:translate-x-0 ${
          isMobileOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        {/* Brand Header */}
        <div className="h-16 flex items-center justify-between px-5 border-b border-slate-100">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-indigo-600 flex items-center justify-center text-white shadow-sm shadow-indigo-200">
              <BookOpen className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-1.5">
                <span className="font-bold text-slate-900 text-lg tracking-tight">Study Hub</span>
                <span className="px-1.5 py-0.5 text-[10px] font-semibold bg-indigo-50 text-indigo-700 rounded-md border border-indigo-100">THPT</span>
              </div>
              <p className="text-[11px] text-slate-500 font-medium">Học tập & Rèn luyện</p>
            </div>
          </div>
          <button
            id="sidebar-close-mobile-btn"
            onClick={onCloseMobile}
            className="p-1 text-slate-400 hover:text-slate-600 rounded-lg lg:hidden"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Navigation Items List */}
        <div className="flex-1 py-4 px-3 overflow-y-auto space-y-1">
          <div className="px-3 pb-2 text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
            Menu chính
          </div>
          {navItems.map((item) => {
            const Icon = item.icon;
            const isActive = currentPage === item.id;
            return (
              <button
                key={item.id}
                id={`sidebar-nav-${item.id}`}
                onClick={() => handleItemClick(item.id)}
                className={`w-full flex items-center justify-between px-3 py-2.5 rounded-xl text-sm font-medium transition-all duration-150 ${
                  isActive
                    ? 'bg-indigo-600 text-white shadow-sm shadow-indigo-100'
                    : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100/80'
                }`}
              >
                <div className="flex items-center gap-3">
                  <Icon className={`w-4 h-4 shrink-0 ${isActive ? 'text-white' : 'text-slate-500'}`} />
                  <span className="truncate">{item.label}</span>
                </div>
                {item.badge && (
                  <span
                    className={`text-[11px] px-2 py-0.5 rounded-full font-semibold ${
                      isActive
                        ? 'bg-indigo-700 text-indigo-100'
                        : 'bg-slate-100 text-slate-600 border border-slate-200'
                    }`}
                  >
                    {item.badge}
                  </span>
                )}
              </button>
            );
          })}
        </div>

        {/* Study Discipline Streak Card */}
        <div className="px-3 py-2">
          <div className="bg-linear-to-r from-amber-50 to-orange-50 border border-amber-200/80 rounded-xl p-3 flex items-center justify-between shadow-2xs">
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 rounded-lg bg-amber-500/15 flex items-center justify-center text-amber-600">
                <Flame className="w-5 h-5 fill-amber-500 text-amber-600" />
              </div>
              <div>
                <p className="text-xs font-bold text-slate-900">Chuỗi học tập</p>
                <p className="text-[11px] text-amber-800 font-medium">{streakDays} ngày liên tiếp!</p>
              </div>
            </div>
            <span className="text-xs font-extrabold text-amber-700 bg-white/80 px-2 py-0.5 rounded-md border border-amber-200">
              🔥 x{streakDays}
            </span>
          </div>
        </div>

        {/* User Mini Profile & Logout */}
        <div className="p-3 border-t border-slate-100 bg-slate-50/50">
          <div className="flex items-center justify-between gap-2 p-1.5 rounded-xl bg-white border border-slate-200 shadow-2xs">
            <div className="flex items-center gap-2.5 min-w-0">
              <img
                src={user?.avatarUrl || 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150&auto=format&fit=crop&q=80'}
                alt={user?.name || 'Học sinh'}
                className="w-8 h-8 rounded-lg object-cover border border-slate-200 shrink-0"
              />
              <div className="min-w-0">
                <p className="text-xs font-bold text-slate-900 truncate">{user?.name || 'Học sinh'}</p>
                <p className="text-[10px] text-slate-500 truncate">{user?.grade || 'Lớp 11'}</p>
              </div>
            </div>
            <button
              id="sidebar-logout-btn"
              onClick={handleLogout}
              title="Đăng xuất"
              className="p-1.5 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors"
            >
              <LogOut className="w-4 h-4" />
            </button>
          </div>
        </div>
      </aside>
    </>
  );
};
