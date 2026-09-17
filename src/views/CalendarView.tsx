import React, { useState } from 'react';
import { 
  ChevronLeft, 
  ChevronRight, 
  Plus, 
  Calendar as CalendarIcon, 
  Clock, 
  MapPin, 
  CheckCircle2, 
  CheckSquare, 
  Square, 
  Flame, 
  Trash2, 
  Edit3, 
  Filter,
  Users,
  BookOpen,
  FileCheck
} from 'lucide-react';
import { CalendarEvent, EventCategory, UserProfile } from '../types';
import { Modal } from '../components/Modal';
import { ConfirmDialog } from '../components/ConfirmDialog';

interface CalendarViewProps {
  user: UserProfile;
  events: CalendarEvent[];
  onSaveEvent: (event: CalendarEvent) => void;
  onDeleteEvent: (eventId: string) => void;
  onToggleEventComplete: (eventId: string) => void;
  showToast: (msg: string, type?: 'success' | 'error' | 'info') => void;
  isCreateModalOpenExternal?: boolean;
  onCloseCreateModalExternal?: () => void;
}

export const CalendarView: React.FC<CalendarViewProps> = ({
  user,
  events,
  onSaveEvent,
  onDeleteEvent,
  onToggleEventComplete,
  showToast,
  isCreateModalOpenExternal,
  onCloseCreateModalExternal,
}) => {
  // Calendar view mode: 'month' | 'week' | 'day'
  const [viewMode, setViewMode] = useState<'month' | 'week' | 'day'>('month');

  // Active date focus (September 2026 as in prompt)
  const [currentYear] = useState(2026);
  const [currentMonth, setCurrentMonth] = useState(8); // 8 is September (0-indexed)
  const [selectedDate, setSelectedDate] = useState('2026-09-09');

  // Category filter
  const [selectedCategory, setSelectedCategory] = useState<string>('all');

  // Event modal state
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingEvent, setEditingEvent] = useState<CalendarEvent | null>(null);

  // Form fields
  const [formTitle, setFormTitle] = useState('');
  const [formDesc, setFormDesc] = useState('');
  const [formDate, setFormDate] = useState('2026-09-09');
  const [formStartTime, setFormStartTime] = useState('14:00');
  const [formEndTime, setFormEndTime] = useState('15:30');
  const [formCategory, setFormCategory] = useState<EventCategory>('study');
  const [formLocation, setFormLocation] = useState('');

  // Event detail view modal
  const [detailEvent, setDetailEvent] = useState<CalendarEvent | null>(null);

  // Delete dialog
  const [deleteEventConfirmId, setDeleteEventConfirmId] = useState<string | null>(null);

  // Filtered events
  const filteredEvents = events.filter((e) => {
    if (selectedCategory === 'all') return true;
    return e.category === selectedCategory;
  });

  // Calculate today's discipline metrics (for 2026-09-09)
  const todayDateStr = '2026-09-09';
  const todayEvents = events.filter(e => e.date === todayDateStr);
  const plannedTasksCount = todayEvents.length || 5;
  const completedTasksCount = todayEvents.filter(e => e.completed).length;
  const completionRate = plannedTasksCount > 0 ? Math.round((completedTasksCount / plannedTasksCount) * 100) : 0;

  // External trigger handler
  React.useEffect(() => {
    if (isCreateModalOpenExternal) {
      handleOpenCreate('2026-09-09');
      if (onCloseCreateModalExternal) onCloseCreateModalExternal();
    }
  }, [isCreateModalOpenExternal]);

  const handleOpenCreate = (initialDate?: string) => {
    setEditingEvent(null);
    setFormTitle('');
    setFormDesc('');
    setFormDate(initialDate || selectedDate);
    setFormStartTime('14:00');
    setFormEndTime('15:30');
    setFormCategory('study');
    setFormLocation('Bàn học / Lớp 11A1');
    setIsModalOpen(true);
  };

  const handleOpenEdit = (evt: CalendarEvent) => {
    setEditingEvent(evt);
    setFormTitle(evt.title);
    setFormDesc(evt.description || '');
    setFormDate(evt.date);
    setFormStartTime(evt.startTime);
    setFormEndTime(evt.endTime);
    setFormCategory(evt.category);
    setFormLocation(evt.location || '');
    setDetailEvent(null);
    setIsModalOpen(true);
  };

  const handleSaveForm = (e: React.FormEvent) => {
    e.preventDefault();
    if (!formTitle.trim()) {
      showToast('Vui lòng nhập tên sự kiện / bài tập.', 'error');
      return;
    }

    const newEvent: CalendarEvent = {
      id: editingEvent ? editingEvent.id : `evt-${Date.now()}`,
      title: formTitle.trim(),
      description: formDesc.trim(),
      date: formDate,
      startTime: formStartTime,
      endTime: formEndTime,
      category: formCategory,
      location: formLocation.trim() || undefined,
      completed: editingEvent ? editingEvent.completed : false,
    };

    onSaveEvent(newEvent);
    setIsModalOpen(false);
    showToast(editingEvent ? 'Đã cập nhật sự kiện thành công!' : 'Đã thêm lịch học mới!', 'success');
  };

  const getCategoryBadge = (cat: EventCategory) => {
    switch (cat) {
      case 'homework':
        return { label: 'Bài tập', bg: 'bg-violet-100 text-violet-800 border-violet-200' };
      case 'exam':
        return { label: 'Thi / Kiểm tra', bg: 'bg-rose-100 text-rose-800 border-rose-200' };
      case 'group':
        return { label: 'Học nhóm', bg: 'bg-emerald-100 text-emerald-800 border-emerald-200' };
      case 'study':
      default:
        return { label: 'Tự học', bg: 'bg-blue-100 text-blue-800 border-blue-200' };
    }
  };

  // Month grid calculations for September 2026
  // Sep 1, 2026 is a Tuesday (index 1 if Monday is start)
  const daysInMonth = 30;
  const days = Array.from({ length: daysInMonth }, (_, i) => i + 1);
  const startDayOfWeek = 1; // Tuesday (Monday = 0, Tuesday = 1, ...)

  return (
    <div id="calendar-view" className="space-y-6 max-w-7xl mx-auto pb-10">
      {/* 1. Study Discipline Header Card */}
      <div id="calendar-discipline-section" className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs">
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div className="flex items-center gap-3.5">
            <div className="w-12 h-12 rounded-2xl bg-amber-500/15 flex items-center justify-center text-amber-600 shrink-0">
              <Flame className="w-7 h-7 fill-amber-500 text-amber-600" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-base font-bold text-slate-900">Kỷ luật học tập hôm nay (Study Discipline)</h3>
                <span className="text-xs font-bold text-amber-800 bg-amber-100 px-2 py-0.5 rounded-full border border-amber-200">
                  Chuỗi {user.studyStreakDays} ngày
                </span>
              </div>
              <p className="text-xs text-slate-500 mt-0.5">
                Kế hoạch hôm nay: <strong className="text-slate-800">{plannedTasksCount} nhiệm vụ</strong> • Đã hoàn thành: <strong className="text-emerald-700">{completedTasksCount}</strong> • Tỷ lệ: <strong className="text-indigo-600">{completionRate}%</strong>
              </p>
            </div>
          </div>

          <div className="flex items-center gap-4 min-w-[260px]">
            <div className="flex-1">
              <div className="flex justify-between text-xs font-semibold mb-1">
                <span className="text-slate-500">Mục tiêu ngày</span>
                <span className="text-emerald-600 font-bold">{completionRate}%</span>
              </div>
              <div className="w-full bg-slate-100 h-2.5 rounded-full overflow-hidden">
                <div
                  className="bg-linear-to-r from-emerald-500 to-teal-500 h-full rounded-full transition-all duration-300"
                  style={{ width: `${completionRate}%` }}
                />
              </div>
            </div>

            <button
              id="calendar-add-today-task-btn"
              onClick={() => handleOpenCreate('2026-09-09')}
              className="px-3.5 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs flex items-center gap-1.5 shrink-0 transition-colors"
            >
              <Plus className="w-4 h-4" />
              <span>Thêm lịch học</span>
            </button>
          </div>
        </div>
      </div>

      {/* 2. Main Calendar Toolbar */}
      <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-2xs flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-4">
        {/* Navigation buttons: < Previous, Today, Next > */}
        <div className="flex items-center gap-2">
          <div className="flex items-center bg-slate-100 p-1 rounded-xl">
            <button
              id="calendar-prev-btn"
              onClick={() => setCurrentMonth(prev => (prev === 0 ? 11 : prev - 1))}
              className="p-1.5 text-slate-600 hover:text-slate-900 hover:bg-white rounded-lg transition-colors"
              title="Tháng trước"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
            <button
              id="calendar-today-btn"
              onClick={() => {
                setCurrentMonth(8);
                setSelectedDate('2026-09-09');
              }}
              className="px-3 py-1 text-xs font-bold text-slate-700 hover:bg-white rounded-lg transition-colors"
            >
              Hôm nay
            </button>
            <button
              id="calendar-next-btn"
              onClick={() => setCurrentMonth(prev => (prev === 11 ? 0 : prev + 1))}
              className="p-1.5 text-slate-600 hover:text-slate-900 hover:bg-white rounded-lg transition-colors"
              title="Tháng sau"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>

          <h2 className="text-base sm:text-lg font-bold text-slate-900 pl-2">
            Tháng 9, 2026
          </h2>
        </div>

        {/* View Mode Toggle & Category Filter */}
        <div className="flex items-center gap-2 flex-wrap">
          {/* Category Filter */}
          <select
            value={selectedCategory}
            onChange={(e) => setSelectedCategory(e.target.value)}
            className="px-3 py-1.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-medium text-slate-700 outline-none"
          >
            <option value="all">Tất cả danh mục</option>
            <option value="study">Tự học</option>
            <option value="homework">Bài tập về nhà</option>
            <option value="exam">Thi / Kiểm tra</option>
            <option value="group">Học nhóm</option>
          </select>

          {/* Month / Week / Day views */}
          <div className="flex items-center bg-slate-100 p-1 rounded-xl">
            <button
              id="calendar-view-month"
              onClick={() => setViewMode('month')}
              className={`px-3 py-1 text-xs font-bold rounded-lg transition-all ${
                viewMode === 'month' ? 'bg-white text-indigo-600 shadow-2xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Tháng
            </button>
            <button
              id="calendar-view-week"
              onClick={() => setViewMode('week')}
              className={`px-3 py-1 text-xs font-bold rounded-lg transition-all ${
                viewMode === 'week' ? 'bg-white text-indigo-600 shadow-2xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Tuần
            </button>
            <button
              id="calendar-view-day"
              onClick={() => setViewMode('day')}
              className={`px-3 py-1 text-xs font-bold rounded-lg transition-all ${
                viewMode === 'day' ? 'bg-white text-indigo-600 shadow-2xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Ngày
            </button>
          </div>
        </div>
      </div>

      {/* 3. Calendar View: MONTH VIEW */}
      {viewMode === 'month' && (
        <div className="bg-white rounded-2xl border border-slate-200 shadow-2xs overflow-hidden">
          {/* Weekday headers */}
          <div className="grid grid-cols-7 border-b border-slate-200 bg-slate-50 text-center text-xs font-bold text-slate-500 py-2.5">
            <div>Thứ Hai</div>
            <div>Thứ Ba</div>
            <div>Thứ Tư</div>
            <div>Thứ Năm</div>
            <div>Thứ Sáu</div>
            <div className="text-amber-700">Thứ Bảy</div>
            <div className="text-rose-700">Chủ Nhật</div>
          </div>

          {/* Calendar Grid */}
          <div className="grid grid-cols-7 auto-rows-[110px] sm:auto-rows-[125px] divide-x divide-y divide-slate-100">
            {/* Blank offset for September 2026 (Starts on Tuesday) */}
            {Array.from({ length: startDayOfWeek }).map((_, idx) => (
              <div key={`offset-${idx}`} className="bg-slate-50/50 p-1.5 opacity-50" />
            ))}

            {/* Days 1 to 30 */}
            {days.map((dayNum) => {
              const dayStr = dayNum < 10 ? `0${dayNum}` : `${dayNum}`;
              const dateStr = `2026-09-${dayStr}`;
              const isToday = dateStr === '2026-09-09';
              const isSelected = selectedDate === dateStr;
              const dayEvents = filteredEvents.filter(e => e.date === dateStr);

              return (
                <div
                  key={dayNum}
                  onClick={() => setSelectedDate(dateStr)}
                  className={`p-1.5 sm:p-2 cursor-pointer transition-colors flex flex-col justify-between overflow-hidden relative ${
                    isToday ? 'bg-indigo-50/30' : 'hover:bg-slate-50'
                  } ${isSelected ? 'ring-2 ring-indigo-500 ring-inset' : ''}`}
                >
                  <div className="flex items-center justify-between">
                    <span className={`text-xs font-bold rounded-full w-6 h-6 flex items-center justify-center ${
                      isToday
                        ? 'bg-indigo-600 text-white shadow-2xs'
                        : 'text-slate-700'
                    }`}>
                      {dayNum}
                    </span>
                    {dayEvents.length > 0 && (
                      <span className="text-[10px] text-slate-400 font-semibold hidden sm:inline">
                        {dayEvents.length} lịch
                      </span>
                    )}
                  </div>

                  {/* Event Chips */}
                  <div className="mt-1 space-y-1 flex-1 overflow-hidden">
                    {dayEvents.slice(0, 2).map((evt) => {
                      const badge = getCategoryBadge(evt.category);
                      return (
                        <div
                          key={evt.id}
                          onClick={(e) => {
                            e.stopPropagation();
                            setDetailEvent(evt);
                          }}
                          className={`px-1.5 py-0.5 rounded text-[10px] font-semibold truncate border ${badge.bg} ${
                            evt.completed ? 'line-through opacity-60' : ''
                          }`}
                          title={`${evt.title} (${evt.startTime})`}
                        >
                          <span className="font-mono mr-1">{evt.startTime}</span>
                          <span>{evt.title}</span>
                        </div>
                      );
                    })}
                    {dayEvents.length > 2 && (
                      <div className="text-[10px] font-semibold text-slate-500 pl-1">
                        + {dayEvents.length - 2} sự kiện nữa
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* 4. Calendar View: WEEK VIEW */}
      {viewMode === 'week' && (
        <div className="bg-white rounded-2xl border border-slate-200 shadow-2xs p-5">
          <div className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-3">
            Lịch học Tuần 2 (07/09 - 13/09, 2026)
          </div>
          <div className="grid grid-cols-1 md:grid-cols-7 gap-3">
            {['2026-09-07', '2026-09-08', '2026-09-09', '2026-09-10', '2026-09-11', '2026-09-12', '2026-09-13'].map((d, idx) => {
              const weekDayLabels = ['Thứ 2 (07/09)', 'Thứ 3 (08/09)', 'Thứ 4 (09/09)', 'Thứ 5 (10/09)', 'Thứ 6 (11/09)', 'Thứ 7 (12/09)', 'CN (13/09)'];
              const isToday = d === '2026-09-09';
              const dayEvts = filteredEvents.filter(e => e.date === d);

              return (
                <div key={d} className={`p-3 rounded-xl border ${isToday ? 'bg-indigo-50/40 border-indigo-300' : 'bg-slate-50/50 border-slate-200'}`}>
                  <div className="flex items-center justify-between pb-2 border-b border-slate-200/60 mb-2">
                    <span className={`text-xs font-bold ${isToday ? 'text-indigo-600' : 'text-slate-700'}`}>
                      {weekDayLabels[idx]}
                    </span>
                    {isToday && <span className="w-2 h-2 rounded-full bg-indigo-600" />}
                  </div>

                  <div className="space-y-2">
                    {dayEvts.map((e) => {
                      const badge = getCategoryBadge(e.category);
                      return (
                        <div
                          key={e.id}
                          onClick={() => setDetailEvent(e)}
                          className={`p-2 rounded-lg border text-xs cursor-pointer ${badge.bg} ${e.completed ? 'line-through opacity-70' : ''}`}
                        >
                          <div className="font-mono text-[10px] text-slate-500">{e.startTime} - {e.endTime}</div>
                          <div className="font-bold text-slate-900 mt-0.5 line-clamp-2">{e.title}</div>
                        </div>
                      );
                    })}
                    {dayEvts.length === 0 && (
                      <p className="text-[11px] text-slate-400 text-center py-4">Không có lịch</p>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* 5. Calendar View: DAY VIEW */}
      {viewMode === 'day' && (
        <div className="bg-white rounded-2xl border border-slate-200 shadow-2xs p-6">
          <div className="flex items-center justify-between pb-4 border-b border-slate-100">
            <div>
              <h3 className="text-base font-bold text-slate-900">Chi tiết ngày {selectedDate}</h3>
              <p className="text-xs text-slate-500">Các hoạt động và bài tập học sinh trong ngày</p>
            </div>
            <button
              onClick={() => handleOpenCreate(selectedDate)}
              className="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl"
            >
              + Thêm cho ngày này
            </button>
          </div>

          <div className="mt-4 space-y-3">
            {filteredEvents.filter(e => e.date === selectedDate).length === 0 ? (
              <div className="p-8 text-center">
                <CalendarIcon className="w-10 h-10 text-slate-300 mx-auto mb-2" />
                <p className="text-xs font-semibold text-slate-600">Không có lịch học nào vào ngày này</p>
                <button
                  onClick={() => handleOpenCreate(selectedDate)}
                  className="mt-3 px-3 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold rounded-xl"
                >
                  Lên kế hoạch ngay
                </button>
              </div>
            ) : (
              filteredEvents.filter(e => e.date === selectedDate).map((evt) => {
                const badge = getCategoryBadge(evt.category);
                return (
                  <div
                    key={evt.id}
                    onClick={() => setDetailEvent(evt)}
                    className="p-4 rounded-xl border border-slate-200 hover:border-indigo-300 hover:shadow-xs transition-all flex items-start justify-between gap-4 cursor-pointer"
                  >
                    <div className="flex items-start gap-3">
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          onToggleEventComplete(evt.id);
                        }}
                        className="mt-0.5 text-slate-400 hover:text-indigo-600"
                      >
                        {evt.completed ? <CheckSquare className="w-5 h-5 text-emerald-600" /> : <Square className="w-5 h-5" />}
                      </button>
                      <div>
                        <div className="flex items-center gap-2 mb-1">
                          <span className={`text-[10px] font-bold px-2 py-0.5 rounded-md border ${badge.bg}`}>
                            {badge.label}
                          </span>
                          <span className="text-xs font-mono text-slate-500">{evt.startTime} - {evt.endTime}</span>
                        </div>
                        <h4 className={`text-sm font-bold ${evt.completed ? 'line-through text-slate-400' : 'text-slate-900'}`}>
                          {evt.title}
                        </h4>
                        {evt.description && <p className="text-xs text-slate-500 mt-1">{evt.description}</p>}
                        {evt.location && (
                          <div className="flex items-center gap-1 text-[11px] text-slate-400 mt-1.5">
                            <MapPin className="w-3 h-3" />
                            <span>{evt.location}</span>
                          </div>
                        )}
                      </div>
                    </div>

                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        setDetailEvent(evt);
                      }}
                      className="text-xs text-indigo-600 hover:text-indigo-800 font-semibold px-2.5 py-1 bg-indigo-50 rounded-lg shrink-0"
                    >
                      Chi tiết
                    </button>
                  </div>
                );
              })
            )}
          </div>
        </div>
      )}

      {/* Create / Edit Event Modal */}
      <Modal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        title={editingEvent ? 'Chỉnh sửa sự kiện / lịch học' : 'Tạo sự kiện học tập mới'}
        subtitle="Quản lý lịch làm bài tập, thi và học nhóm"
      >
        <form onSubmit={handleSaveForm} className="space-y-4">
          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">Tiêu đề lịch học</label>
            <input
              type="text"
              required
              placeholder="Ví dụ: Bài tập Toán đại số, Kiểm tra 15p Lý..."
              value={formTitle}
              onChange={(e) => setFormTitle(e.target.value)}
              className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:border-indigo-500 outline-none"
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Danh mục</label>
              <select
                value={formCategory}
                onChange={(e) => setFormCategory(e.target.value as EventCategory)}
                className="w-full px-3 py-2 text-xs text-slate-800 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:border-indigo-500 outline-none"
              >
                <option value="study">Tự học (Study)</option>
                <option value="homework">Bài tập về nhà (Homework)</option>
                <option value="exam">Thi / Kiểm tra (Exam)</option>
                <option value="group">Học nhóm (Group Study)</option>
              </select>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Ngày diễn ra</label>
              <input
                type="date"
                required
                value={formDate}
                onChange={(e) => setFormDate(e.target.value)}
                className="w-full px-3 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:border-indigo-500 outline-none"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Giờ bắt đầu</label>
              <input
                type="time"
                value={formStartTime}
                onChange={(e) => setFormStartTime(e.target.value)}
                className="w-full px-3 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none"
              />
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Giờ kết thúc</label>
              <input
                type="time"
                value={formEndTime}
                onChange={(e) => setFormEndTime(e.target.value)}
                className="w-full px-3 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">Địa điểm / Đường dẫn phòng học (Tùy chọn)</label>
            <input
              type="text"
              placeholder="Ví dụ: Phòng 302, Thư viện, Google Meet..."
              value={formLocation}
              onChange={(e) => setFormLocation(e.target.value)}
              className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:border-indigo-500 outline-none"
            />
          </div>

          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">Mô tả / Hướng dẫn chuẩn bị</label>
            <textarea
              rows={2}
              placeholder="Ghi chú bài tập cần chuẩn bị hoặc tài liệu tham khảo..."
              value={formDesc}
              onChange={(e) => setFormDesc(e.target.value)}
              className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:border-indigo-500 outline-none"
            />
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
            <button
              type="button"
              onClick={() => setIsModalOpen(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl"
            >
              Hủy
            </button>
            <button
              type="submit"
              className="px-4 py-2 text-xs font-semibold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl shadow-2xs"
            >
              {editingEvent ? 'Lưu cập nhật' : 'Thêm vào lịch'}
            </button>
          </div>
        </form>
      </Modal>

      {/* Event Detail Modal */}
      {detailEvent && (
        <Modal
          isOpen={true}
          onClose={() => setDetailEvent(null)}
          title={detailEvent.title}
          subtitle={`Chi tiết lịch học • ${detailEvent.date}`}
          maxWidth="md"
        >
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <span className={`text-xs font-bold px-2.5 py-1 rounded-lg border ${getCategoryBadge(detailEvent.category).bg}`}>
                {getCategoryBadge(detailEvent.category).label}
              </span>
              <button
                onClick={() => {
                  onToggleEventComplete(detailEvent.id);
                  setDetailEvent({ ...detailEvent, completed: !detailEvent.completed });
                  showToast(!detailEvent.completed ? 'Đã đánh dấu hoàn thành!' : 'Đã bỏ đánh dấu hoàn thành', 'info');
                }}
                className={`flex items-center gap-1.5 px-3 py-1 text-xs font-bold rounded-lg border transition-colors ${
                  detailEvent.completed
                    ? 'bg-emerald-100 text-emerald-800 border-emerald-300'
                    : 'bg-slate-100 text-slate-700 border-slate-200 hover:bg-slate-200'
                }`}
              >
                {detailEvent.completed ? <CheckSquare className="w-4 h-4 text-emerald-600" /> : <Square className="w-4 h-4" />}
                <span>{detailEvent.completed ? 'Đã hoàn thành' : 'Đánh dấu hoàn thành'}</span>
              </button>
            </div>

            <div className="space-y-2 text-xs text-slate-600">
              <div className="flex items-center gap-2">
                <Clock className="w-4 h-4 text-slate-400 shrink-0" />
                <span>Thời gian: <strong>{detailEvent.startTime} - {detailEvent.endTime}</strong> ngày <strong>{detailEvent.date}</strong></span>
              </div>
              {detailEvent.location && (
                <div className="flex items-center gap-2">
                  <MapPin className="w-4 h-4 text-slate-400 shrink-0" />
                  <span>Địa điểm: <strong>{detailEvent.location}</strong></span>
                </div>
              )}
            </div>

            {detailEvent.description && (
              <div className="p-3 bg-slate-50 rounded-xl border border-slate-200 text-xs text-slate-700 leading-relaxed">
                <p className="font-semibold text-slate-800 mb-1">Nội dung ghi chú:</p>
                <p>{detailEvent.description}</p>
              </div>
            )}

            <div className="pt-3 border-t border-slate-100 flex items-center justify-between">
              <button
                onClick={() => {
                  setDeleteEventConfirmId(detailEvent.id);
                  setDetailEvent(null);
                }}
                className="text-xs text-rose-600 hover:text-rose-800 font-semibold flex items-center gap-1"
              >
                <Trash2 className="w-3.5 h-3.5" />
                <span>Xóa sự kiện</span>
              </button>

              <div className="flex items-center gap-2">
                <button
                  onClick={() => handleOpenEdit(detailEvent)}
                  className="px-3.5 py-1.5 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 text-xs font-semibold rounded-xl border border-indigo-200 flex items-center gap-1"
                >
                  <Edit3 className="w-3.5 h-3.5" />
                  <span>Chỉnh sửa</span>
                </button>
                <button
                  onClick={() => setDetailEvent(null)}
                  className="px-3.5 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold rounded-xl"
                >
                  Đóng
                </button>
              </div>
            </div>
          </div>
        </Modal>
      )}

      {/* Delete event confirmation */}
      <ConfirmDialog
        isOpen={!!deleteEventConfirmId}
        onClose={() => setDeleteEventConfirmId(null)}
        onConfirm={() => {
          if (deleteEventConfirmId) {
            onDeleteEvent(deleteEventConfirmId);
            showToast('Đã xóa sự kiện khỏi lịch.', 'info');
            setDeleteEventConfirmId(null);
          }
        }}
        title="Xóa sự kiện này?"
        message="Lịch học hoặc bài tập này sẽ bị xóa khỏi thời khóa biểu của bạn."
        confirmText="Xóa sự kiện"
        cancelText="Hủy"
        variant="danger"
        iconType="delete"
      />
    </div>
  );
};
