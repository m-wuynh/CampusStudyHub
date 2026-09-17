import React, { useState, useRef, useEffect } from 'react';
import { 
  Menu, 
  Search, 
  Bell, 
  Plus, 
  Check, 
  Clock, 
  FileText, 
  Layers, 
  Calendar as CalendarIcon, 
  GraduationCap, 
  CheckCircle2, 
  X
} from 'lucide-react';
import { NavigationPage, UserProfile } from '../types';

interface TopBarProps {
  currentPage: NavigationPage;
  user?: UserProfile | null;
  onOpenMobileMenu?: () => void;
  onQuickAction: (action: 'add-note' | 'add-flashcard' | 'add-event' | 'add-grade') => void;
  searchQuery?: string;
  onSearchChange?: (q: string) => void;
}

export const TopBar: React.FC<TopBarProps> = ({
  currentPage,
  user,
  onOpenMobileMenu = () => {},
  onQuickAction,
  searchQuery = '',
  onSearchChange = (_q: string) => {},
}) => {
  const [showNotifications, setShowNotifications] = useState(false);
  const [showQuickMenu, setShowQuickMenu] = useState(false);
  const [notifications, setNotifications] = useState([
    {
      id: 'notif-1',
      title: 'Kiểm tra 15 phút Vật lý',
      time: 'Thứ Sáu, 11/09 lúc 08:45',
      unread: true,
      type: 'exam',
    },
    {
      id: 'notif-2',
      title: 'Nhóm Toán Chuyên sâu 11A1',
      time: 'Nguyễn Minh Anh vừa đăng thông báo mới',
      unread: true,
      type: 'group',
    },
    {
      id: 'notif-3',
      title: 'Nhắc nhở học tập: Lượng giác',
      time: 'Hoàn thành 1 thẻ flashcard còn dở',
      unread: false,
      type: 'study',
    },
  ]);

  const notifRef = useRef<HTMLDivElement>(null);
  const quickRef = useRef<HTMLDivElement>(null);

  // Close dropdowns on click outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (notifRef.current && !notifRef.current.contains(e.target as Node)) {
        setShowNotifications(false);
      }
      if (quickRef.current && !quickRef.current.contains(e.target as Node)) {
        setShowQuickMenu(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const getPageInfo = () => {
    switch (currentPage) {
      case 'dashboard':
        return { title: 'Bảng điều khiển học tập', subtitle: 'Tổng hợp tiến độ & hoạt động hôm nay' };
      case 'notes':
        return { title: 'Ghi chú & Tài liệu học', subtitle: 'Tổ chức bài học theo môn và định dạng' };
      case 'flashcards':
        return { title: 'Thẻ ghi nhớ (Flashcards)', subtitle: 'Ôn tập kiến thức & chia sẻ bộ thẻ' };
      case 'calendar':
        return { title: 'Thời khóa biểu & Kế hoạch', subtitle: 'Theo dõi lịch học, bài tập và kỳ thi' };
      case 'groups':
        return { title: 'Nhóm học tập', subtitle: 'Cùng trao đổi kiến thức với bạn cùng lớp' };
      case 'grades':
        return { title: 'Điểm số & Mục tiêu học tập', subtitle: 'Theo dõi điểm trung bình và biểu đồ tiến bộ' };
      case 'settings':
        return { title: 'Cài đặt tài khoản', subtitle: 'Tùy chỉnh thông tin và giao diện học tập' };
      default:
        return { title: 'Study Hub', subtitle: 'Nền tảng học tập' };
    }
  };

  const pageInfo = getPageInfo();
  const unreadCount = notifications.filter(n => n.unread).length;

  const markAllAsRead = () => {
    setNotifications(prev => prev.map(n => ({ ...n, unread: false })));
  };

  return (
    <header id="app-topbar" className="h-16 bg-white border-b border-slate-200 sticky top-0 z-30 px-4 lg:px-8 flex items-center justify-between">
      {/* Left side: Hamburger & Title */}
      <div className="flex items-center gap-3">
        <button
          id="topbar-hamburger-btn"
          onClick={onOpenMobileMenu}
          className="p-2 text-slate-500 hover:text-slate-800 hover:bg-slate-100 rounded-xl lg:hidden"
          title="Mở menu"
        >
          <Menu className="w-5 h-5" />
        </button>

        <div>
          <h1 className="text-base sm:text-lg font-bold text-slate-900 leading-tight">
            {pageInfo.title}
          </h1>
          <p className="hidden sm:block text-xs text-slate-500 font-normal">
            {pageInfo.subtitle}
          </p>
        </div>
      </div>

      {/* Right side: Search, Quick Add, Notifications, User */}
      <div className="flex items-center gap-2 sm:gap-3">
        {/* Search Bar */}
        <div className="relative hidden md:block w-64">
          <Search className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none" />
          <input
            id="topbar-search-input"
            type="text"
            placeholder="Tìm ghi chú, thẻ, sự kiện..."
            value={searchQuery}
            onChange={(e) => onSearchChange(e.target.value)}
            className="w-full pl-9 pr-8 py-1.5 bg-slate-100/80 hover:bg-slate-100 focus:bg-white text-xs text-slate-800 placeholder-slate-400 rounded-xl border border-transparent focus:border-indigo-300 focus:ring-2 focus:ring-indigo-100 outline-none transition-all"
          />
          {searchQuery && (
            <button
              onClick={() => onSearchChange('')}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 p-0.5"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          )}
        </div>

        {/* Quick Add Dropdown */}
        <div className="relative" ref={quickRef}>
          <button
            id="topbar-quick-add-btn"
            onClick={() => setShowQuickMenu(!showQuickMenu)}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-xs transition-colors"
          >
            <Plus className="w-4 h-4" />
            <span className="hidden sm:inline">Tạo mới</span>
          </button>

          {showQuickMenu && (
            <div className="absolute right-0 mt-2 w-52 bg-white rounded-2xl shadow-xl border border-slate-200 py-1.5 z-50 animate-in fade-in-50 zoom-in-95">
              <div className="px-3 py-1.5 text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                Thêm nhanh
              </div>
              <button
                id="quick-add-note-btn"
                onClick={() => {
                  setShowQuickMenu(false);
                  onQuickAction('add-note');
                }}
                className="w-full px-3 py-2 text-left text-xs text-slate-700 hover:bg-slate-50 flex items-center gap-2.5"
              >
                <FileText className="w-4 h-4 text-indigo-600" />
                <span>Thêm ghi chú mới</span>
              </button>
              <button
                id="quick-add-flashcard-btn"
                onClick={() => {
                  setShowQuickMenu(false);
                  onQuickAction('add-flashcard');
                }}
                className="w-full px-3 py-2 text-left text-xs text-slate-700 hover:bg-slate-50 flex items-center gap-2.5"
              >
                <Layers className="w-4 h-4 text-emerald-600" />
                <span>Tạo bộ Flashcard</span>
              </button>
              <button
                id="quick-add-event-btn"
                onClick={() => {
                  setShowQuickMenu(false);
                  onQuickAction('add-event');
                }}
                className="w-full px-3 py-2 text-left text-xs text-slate-700 hover:bg-slate-50 flex items-center gap-2.5"
              >
                <CalendarIcon className="w-4 h-4 text-amber-600" />
                <span>Lên lịch học / Kiểm tra</span>
              </button>
              <button
                id="quick-add-grade-btn"
                onClick={() => {
                  setShowQuickMenu(false);
                  onQuickAction('add-grade');
                }}
                className="w-full px-3 py-2 text-left text-xs text-slate-700 hover:bg-slate-50 flex items-center gap-2.5 border-t border-slate-100"
              >
                <GraduationCap className="w-4 h-4 text-rose-600" />
                <span>Nhập điểm bài thi</span>
              </button>
            </div>
          )}
        </div>

        {/* Notifications Popover */}
        <div className="relative" ref={notifRef}>
          <button
            id="topbar-notifications-btn"
            onClick={() => setShowNotifications(!showNotifications)}
            className="p-2 text-slate-500 hover:text-slate-800 hover:bg-slate-100 rounded-xl relative transition-colors"
            title="Thông báo"
          >
            <Bell className="w-5 h-5" />
            {unreadCount > 0 && (
              <span className="absolute top-1.5 right-1.5 w-2.5 h-2.5 bg-rose-500 rounded-full ring-2 ring-white animate-pulse" />
            )}
          </button>

          {showNotifications && (
            <div className="absolute right-0 mt-2 w-80 sm:w-96 bg-white rounded-2xl shadow-xl border border-slate-200 py-2 z-50 animate-in fade-in-50 zoom-in-95">
              <div className="flex items-center justify-between px-4 py-2 border-b border-slate-100">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-bold text-slate-900">Thông báo học tập</span>
                  {unreadCount > 0 && (
                    <span className="px-1.5 py-0.5 text-[10px] font-bold bg-rose-100 text-rose-700 rounded-full">
                      {unreadCount} mới
                    </span>
                  )}
                </div>
                {unreadCount > 0 && (
                  <button
                    onClick={markAllAsRead}
                    className="text-[11px] text-indigo-600 hover:text-indigo-800 font-medium flex items-center gap-1"
                  >
                    <Check className="w-3 h-3" /> Đã xem tất cả
                  </button>
                )}
              </div>

              <div className="divide-y divide-slate-100 max-h-72 overflow-y-auto">
                {notifications.map((n) => (
                  <div
                    key={n.id}
                    className={`p-3 text-xs transition-colors hover:bg-slate-50 flex items-start gap-3 ${
                      n.unread ? 'bg-indigo-50/40' : ''
                    }`}
                  >
                    <div className="p-2 rounded-lg bg-slate-100 shrink-0 text-slate-600 mt-0.5">
                      {n.type === 'exam' && <Clock className="w-4 h-4 text-rose-600" />}
                      {n.type === 'group' && <Layers className="w-4 h-4 text-indigo-600" />}
                      {n.type === 'study' && <CheckCircle2 className="w-4 h-4 text-emerald-600" />}
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className={`font-semibold ${n.unread ? 'text-slate-900' : 'text-slate-700'}`}>
                        {n.title}
                      </p>
                      <p className="text-[11px] text-slate-500 mt-0.5 leading-snug">{n.time}</p>
                    </div>
                    {n.unread && (
                      <span className="w-2 h-2 rounded-full bg-indigo-600 shrink-0 mt-1.5" />
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* User Avatar & Name */}
        <div className="flex items-center gap-2.5 pl-2 border-l border-slate-200">
          <img
            src={user?.avatarUrl || 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150&auto=format&fit=crop&q=80'}
            alt={user?.name || 'Học sinh'}
            className="w-8 h-8 rounded-full object-cover ring-2 ring-indigo-100 shrink-0"
          />
          <div className="hidden xl:block text-left">
            <p className="text-xs font-bold text-slate-900 leading-tight">{user?.name || 'Học sinh'}</p>
            <p className="text-[10px] text-slate-500">{user?.grade || 'Lớp 11'}</p>
          </div>
        </div>
      </div>
    </header>
  );
};
