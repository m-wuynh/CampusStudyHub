import React from 'react';
import { 
  FileText, 
  Layers, 
  Calendar as CalendarIcon, 
  GraduationCap, 
  Plus, 
  CheckCircle2, 
  Clock, 
  Flame, 
  ArrowRight, 
  CheckSquare, 
  Square,
  TrendingUp,
  AlertCircle,
  Users
} from 'lucide-react';
import { 
  UserProfile, 
  Note, 
  FlashcardSet, 
  CalendarEvent, 
  StudyGroup, 
  GradeEntry, 
  GradeRecord,
  AcademicGoal, 
  NavigationPage 
} from '../types';
import { subjectColorMap } from '../mockData';

interface DashboardViewProps {
  user: UserProfile;
  notes: Note[];
  flashcardSets: FlashcardSet[];
  events: CalendarEvent[];
  groups: StudyGroup[];
  grades: (GradeRecord | GradeEntry)[];
  goals: AcademicGoal[];
  onNavigate: (page: NavigationPage) => void;
  onToggleEventComplete: (eventId: string) => void;
  onQuickAction?: (action: 'add-note' | 'add-flashcard' | 'add-event' | 'add-grade') => void;
  onOpenStudySet: (setId: string) => void;
  onOpenNote: (noteId: string) => void;
}

export const DashboardView: React.FC<DashboardViewProps> = ({
  user,
  notes,
  flashcardSets,
  events,
  groups,
  grades,
  goals,
  onNavigate,
  onToggleEventComplete,
  onQuickAction,
  onOpenStudySet,
  onOpenNote,
}) => {
  // Today's events (filtered by today 2026-09-09 or closest)
  const todayEvents = events.filter(e => e.date === '2026-09-09');
  const totalTodayTasks = todayEvents.length || 4;
  const completedTodayTasks = todayEvents.filter(e => e.completed).length;
  const disciplineRate = Math.round((completedTodayTasks / totalTodayTasks) * 100);

  // Calculate current average grade
  const currentGPA = grades.length > 0
    ? (grades.reduce((acc, g) => {
        if ('averageScore' in g) {
          return acc + g.averageScore;
        } else if ('score' in g) {
          return acc + g.score;
        }
        return acc;
      }, 0) / grades.length).toFixed(1)
    : '8.4';
  const targetGPA = user.targetGpa ? user.targetGpa.toFixed(1) : '8.8';
  const gpaProgress = Math.min(100, Math.round((parseFloat(currentGPA) / parseFloat(targetGPA)) * 100));

  // Upcoming items (tomorrow and later)
  const upcomingEvents = events.filter(e => e.date > '2026-09-09').slice(0, 4);

  return (
    <div id="dashboard-view" className="space-y-6 max-w-7xl mx-auto pb-10">
      {/* 1. Welcome Card */}
      <div 
        id="dashboard-welcome-card"
        className="relative overflow-hidden bg-linear-to-r from-indigo-600 via-indigo-700 to-blue-700 rounded-2xl text-white p-6 sm:p-8 shadow-md shadow-indigo-100"
      >
        <div className="relative z-10 max-w-2xl">
          <div className="inline-flex items-center gap-2 px-3 py-1 bg-white/15 backdrop-blur-xs rounded-full text-xs font-semibold text-indigo-100 mb-3 border border-white/20">
            <span>📅 Thứ Tư, 09 tháng 09, 2026</span>
            <span>•</span>
            <span>Học kỳ 1</span>
          </div>
          <h2 className="text-xl sm:text-2xl lg:text-3xl font-black tracking-tight">
            Chào buổi sáng, {user.name} 👋
          </h2>
          <p className="mt-2 text-xs sm:text-sm text-indigo-100/90 leading-relaxed">
            Sẵn sàng tiếp tục buổi học hôm nay chưa? Bạn có <span className="font-bold text-white underline decoration-amber-400 decoration-2">{totalTodayTasks - completedTodayTasks} nhiệm vụ</span> cần hoàn thành và <span className="font-bold text-white underline decoration-rose-400 decoration-2">1 bài kiểm tra</span> sắp tới vào thứ Sáu.
          </p>
        </div>

        {/* Decorative background geometry */}
        <div className="absolute right-0 bottom-0 top-0 w-1/3 opacity-10 pointer-events-none hidden sm:flex items-center justify-center">
          <GraduationCap className="w-64 h-64 text-white -mr-10 -mb-10" />
        </div>
      </div>

      {/* 2. Today's Overview Stats Grid */}
      <div id="dashboard-overview-stats" className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Stat 1: Tasks Today */}
        <div className="bg-white p-4 sm:p-5 rounded-2xl border border-slate-200 shadow-2xs">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-slate-500">Nhiệm vụ hôm nay</span>
            <div className="w-8 h-8 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center">
              <CalendarIcon className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3 flex items-baseline gap-2">
            <span className="text-2xl font-black text-slate-900">{totalTodayTasks}</span>
            <span className="text-xs text-slate-500 font-medium">bài tập / lịch hẹn</span>
          </div>
          <p className="text-[11px] text-blue-600 font-medium mt-1">
            Đã lên lịch đầy đủ
          </p>
        </div>

        {/* Stat 2: Completed tasks */}
        <div className="bg-white p-4 sm:p-5 rounded-2xl border border-slate-200 shadow-2xs">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-slate-500">Đã hoàn thành</span>
            <div className="w-8 h-8 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center">
              <CheckCircle2 className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3 flex items-baseline gap-2">
            <span className="text-2xl font-black text-emerald-600">{completedTodayTasks}</span>
            <span className="text-xs text-slate-500 font-medium">/ {totalTodayTasks} nhiệm vụ</span>
          </div>
          <div className="w-full bg-slate-100 h-1.5 rounded-full mt-2 overflow-hidden">
            <div 
              className="bg-emerald-500 h-full rounded-full transition-all duration-300"
              style={{ width: `${disciplineRate}%` }}
            />
          </div>
        </div>

        {/* Stat 3: Upcoming Exam */}
        <div className="bg-white p-4 sm:p-5 rounded-2xl border border-slate-200 shadow-2xs">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-slate-500">Kỳ thi sắp tới</span>
            <div className="w-8 h-8 rounded-xl bg-rose-50 text-rose-600 flex items-center justify-center">
              <Clock className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3 flex items-baseline gap-1.5">
            <span className="text-lg font-black text-slate-900 truncate">Vật lý 15p</span>
          </div>
          <p className="text-[11px] text-rose-600 font-semibold mt-1 flex items-center gap-1">
            <span>Còn 2 ngày (11/09)</span>
          </p>
        </div>

        {/* Stat 4: Current GPA */}
        <div className="bg-white p-4 sm:p-5 rounded-2xl border border-slate-200 shadow-2xs">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-slate-500">ĐTB Hiện tại</span>
            <div className="w-8 h-8 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center">
              <TrendingUp className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3 flex items-baseline gap-2">
            <span className="text-2xl font-black text-indigo-600">{currentGPA}</span>
            <span className="text-xs text-slate-400 font-medium">/ 10</span>
          </div>
          <p className="text-[11px] text-emerald-600 font-semibold mt-1">
            Xếp loại: Học sinh Giỏi
          </p>
        </div>
      </div>

      {/* 3. Quick Actions Bar */}
      <div id="dashboard-quick-actions" className="bg-white p-4 rounded-2xl border border-slate-200 shadow-2xs">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-xs font-bold text-slate-500 uppercase tracking-wider">
            Thao tác nhanh (Quick Actions)
          </h3>
          <span className="text-[11px] text-slate-400">Nhấn để thêm dữ liệu ngay</span>
        </div>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <button
            id="dash-quick-add-note"
            onClick={() => onQuickAction('add-note')}
            className="flex items-center justify-center gap-2 px-3 py-2.5 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 rounded-xl text-xs font-semibold border border-indigo-100 transition-colors"
          >
            <Plus className="w-4 h-4" />
            <span>Thêm Ghi chú</span>
          </button>
          <button
            id="dash-quick-add-flashcard"
            onClick={() => onQuickAction('add-flashcard')}
            className="flex items-center justify-center gap-2 px-3 py-2.5 bg-emerald-50 hover:bg-emerald-100 text-emerald-700 rounded-xl text-xs font-semibold border border-emerald-100 transition-colors"
          >
            <Plus className="w-4 h-4" />
            <span>Tạo Flashcard</span>
          </button>
          <button
            id="dash-quick-add-schedule"
            onClick={() => onQuickAction('add-event')}
            className="flex items-center justify-center gap-2 px-3 py-2.5 bg-amber-50 hover:bg-amber-100 text-amber-800 rounded-xl text-xs font-semibold border border-amber-200 transition-colors"
          >
            <Plus className="w-4 h-4" />
            <span>Thêm Lịch học</span>
          </button>
          <button
            id="dash-quick-add-grade"
            onClick={() => onQuickAction('add-grade')}
            className="flex items-center justify-center gap-2 px-3 py-2.5 bg-rose-50 hover:bg-rose-100 text-rose-700 rounded-xl text-xs font-semibold border border-rose-100 transition-colors"
          >
            <Plus className="w-4 h-4" />
            <span>Nhập Điểm số</span>
          </button>
        </div>
      </div>

      {/* 4. Two Columns: Study Discipline & Academic Progress */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Discipline Card (Span 2 cols) */}
        <div id="dashboard-discipline-card" className="lg:col-span-2 bg-white rounded-2xl border border-slate-200 p-5 sm:p-6 shadow-2xs">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-100">
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-base font-bold text-slate-900">Kỷ luật học tập hôm nay</h3>
                <span className="px-2 py-0.5 text-[11px] font-bold bg-amber-100 text-amber-800 rounded-full border border-amber-200 flex items-center gap-1">
                  <Flame className="w-3 h-3 fill-amber-500 text-amber-600" />
                  Streak: {user.studyStreakDays} ngày
                </span>
              </div>
              <p className="text-xs text-slate-500 mt-0.5">
                Hoàn thành các nhiệm vụ đã lên lịch để duy trì chuỗi ngày rèn luyện
              </p>
            </div>
            <div className="flex items-center gap-3">
              <div className="text-right">
                <span className="text-sm font-bold text-slate-900">{completedTodayTasks} / {totalTodayTasks}</span>
                <span className="text-xs text-slate-500 ml-1">nhiệm vụ</span>
              </div>
              <div className="w-12 h-12 rounded-full border-4 border-emerald-100 flex items-center justify-center text-xs font-black text-emerald-600">
                {disciplineRate}%
              </div>
            </div>
          </div>

          {/* Progress Bar */}
          <div className="mt-4">
            <div className="flex justify-between text-xs font-medium text-slate-600 mb-1.5">
              <span>Tiến độ hoàn thành hôm nay</span>
              <span className="font-bold text-emerald-600">{disciplineRate}%</span>
            </div>
            <div className="w-full bg-slate-100 h-2.5 rounded-full overflow-hidden">
              <div
                className="bg-linear-to-r from-emerald-500 to-teal-500 h-full rounded-full transition-all duration-300"
                style={{ width: `${disciplineRate}%` }}
              />
            </div>
          </div>

          {/* Interactive Checklist for Today */}
          <div className="mt-5 space-y-2.5">
            <p className="text-xs font-semibold text-slate-500">Danh sách việc cần làm hôm nay:</p>
            {todayEvents.map((evt) => (
              <div
                key={evt.id}
                onClick={() => onToggleEventComplete(evt.id)}
                className={`p-3 rounded-xl border transition-all cursor-pointer flex items-center justify-between gap-3 ${
                  evt.completed
                    ? 'bg-slate-50 border-slate-200 text-slate-400'
                    : 'bg-white border-slate-200 hover:border-indigo-300 hover:shadow-2xs text-slate-800'
                }`}
              >
                <div className="flex items-center gap-3 min-w-0">
                  <button
                    type="button"
                    className="shrink-0 text-slate-400 hover:text-indigo-600"
                  >
                    {evt.completed ? (
                      <CheckSquare className="w-5 h-5 text-emerald-600" />
                    ) : (
                      <Square className="w-5 h-5 text-slate-400" />
                    )}
                  </button>
                  <div className="min-w-0">
                    <p className={`text-xs font-semibold truncate ${evt.completed ? 'line-through text-slate-400' : 'text-slate-900'}`}>
                      {evt.title}
                    </p>
                    <p className="text-[11px] text-slate-500 truncate flex items-center gap-2 mt-0.5">
                      <span>{evt.startTime} - {evt.endTime}</span>
                      {evt.location && <span>• {evt.location}</span>}
                    </p>
                  </div>
                </div>
                <span className={`text-[10px] font-semibold px-2 py-0.5 rounded-md shrink-0 ${
                  evt.category === 'homework' ? 'bg-violet-50 text-violet-700' :
                  evt.category === 'study' ? 'bg-blue-50 text-blue-700' :
                  evt.category === 'exam' ? 'bg-rose-50 text-rose-700' : 'bg-emerald-50 text-emerald-700'
                }`}>
                  {evt.category === 'homework' ? 'Bài tập' :
                   evt.category === 'study' ? 'Tự học' :
                   evt.category === 'exam' ? 'Kiểm tra' : 'Học nhóm'}
                </span>
              </div>
            ))}
          </div>

          <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-xs">
            <span className="text-slate-500">Mẹo: Đạt 100% để duy trì streak lên 6 ngày!</span>
            <button
              onClick={() => onNavigate('calendar')}
              className="text-indigo-600 hover:text-indigo-800 font-semibold flex items-center gap-1"
            >
              <span>Xem lịch đầy đủ</span>
              <ArrowRight className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>

        {/* Academic Progress & Goals widget */}
        <div id="dashboard-academic-progress" className="bg-white rounded-2xl border border-slate-200 p-5 sm:p-6 shadow-2xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h3 className="text-base font-bold text-slate-900">Mục tiêu học tập</h3>
              <button
                onClick={() => onNavigate('grades')}
                className="text-xs text-indigo-600 hover:text-indigo-800 font-semibold"
              >
                Chi tiết
              </button>
            </div>

            {/* GPA comparison */}
            <div className="mt-4 p-4 rounded-xl bg-slate-50 border border-slate-200/80">
              <div className="flex items-center justify-between">
                <div>
                  <span className="text-[11px] font-medium text-slate-500">ĐTB Hiện tại</span>
                  <div className="text-2xl font-black text-slate-900">{currentGPA}</div>
                </div>
                <div className="text-right">
                  <span className="text-[11px] font-medium text-slate-500">Mục tiêu kỳ 1</span>
                  <div className="text-2xl font-black text-indigo-600">{targetGPA}</div>
                </div>
              </div>

              <div className="mt-3">
                <div className="flex justify-between text-[11px] text-slate-500 mb-1">
                  <span>Tiến trình đạt mục tiêu</span>
                  <span className="font-bold text-indigo-600">{gpaProgress}%</span>
                </div>
                <div className="w-full bg-slate-200 h-2 rounded-full overflow-hidden">
                  <div 
                    className="bg-indigo-600 h-full rounded-full transition-all duration-500" 
                    style={{ width: `${gpaProgress}%` }}
                  />
                </div>
              </div>
            </div>

            {/* Top Goals preview */}
            <div className="mt-4 space-y-2.5">
              <span className="text-xs font-semibold text-slate-500">Mục tiêu ưu tiên:</span>
              {goals.slice(0, 3).map((g) => {
                const percent = Math.min(100, Math.round((g.currentScore / g.targetScore) * 100));
                return (
                  <div key={g.id} className="p-2.5 rounded-xl border border-slate-100 bg-white hover:bg-slate-50 transition-colors">
                    <div className="flex items-center justify-between text-xs">
                      <span className="font-semibold text-slate-800 truncate pr-2">{g.title}</span>
                      <span className="font-mono font-bold text-slate-600 shrink-0">{g.currentScore}/{g.targetScore}</span>
                    </div>
                    <div className="w-full bg-slate-100 h-1.5 rounded-full mt-2 overflow-hidden">
                      <div 
                        className={`h-full rounded-full ${g.isCompleted ? 'bg-emerald-500' : 'bg-indigo-600'}`}
                        style={{ width: `${percent}%` }}
                      />
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          <button
            onClick={() => onNavigate('grades')}
            className="mt-4 w-full py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold rounded-xl transition-colors text-center"
          >
            Quản lý bảng điểm & mục tiêu
          </button>
        </div>
      </div>

      {/* 5. Bottom Grid: Upcoming Items & Recent Flashcard Sets */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Upcoming: Homework, Exams, Study sessions */}
        <div id="dashboard-upcoming-section" className="bg-white rounded-2xl border border-slate-200 p-5 sm:p-6 shadow-2xs">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100">
            <div>
              <h3 className="text-base font-bold text-slate-900">Sắp diễn ra (Upcoming)</h3>
              <p className="text-xs text-slate-500 mt-0.5">Bài tập, kỳ thi và buổi học nhóm tuần này</p>
            </div>
            <button
              onClick={() => onNavigate('calendar')}
              className="text-xs text-indigo-600 hover:text-indigo-800 font-semibold flex items-center gap-1"
            >
              <span>Xem lịch</span>
              <ArrowRight className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="mt-4 space-y-3">
            {upcomingEvents.map((evt) => (
              <div
                key={evt.id}
                className="p-3 rounded-xl border border-slate-100 bg-slate-50/50 hover:bg-white hover:border-slate-300 transition-all flex items-start justify-between gap-3"
              >
                <div className="flex items-start gap-3">
                  <div className={`p-2 rounded-xl mt-0.5 shrink-0 ${
                    evt.category === 'exam' ? 'bg-rose-100 text-rose-700' :
                    evt.category === 'homework' ? 'bg-violet-100 text-violet-700' :
                    evt.category === 'group' ? 'bg-emerald-100 text-emerald-700' : 'bg-blue-100 text-blue-700'
                  }`}>
                    {evt.category === 'exam' ? <Clock className="w-4 h-4" /> :
                     evt.category === 'group' ? <Users className="w-4 h-4" /> :
                     <FileText className="w-4 h-4" />}
                  </div>
                  <div>
                    <h4 className="text-xs font-bold text-slate-900">{evt.title}</h4>
                    <p className="text-[11px] text-slate-500 mt-0.5">{evt.description}</p>
                    <div className="flex items-center gap-2 mt-1 text-[11px] font-medium text-slate-600">
                      <span>📅 {evt.date}</span>
                      <span>• {evt.startTime} - {evt.endTime}</span>
                      {evt.location && <span className="text-slate-400">({evt.location})</span>}
                    </div>
                  </div>
                </div>
                <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full shrink-0 ${
                  evt.category === 'exam' ? 'bg-rose-50 text-rose-700 border border-rose-200' :
                  evt.category === 'group' ? 'bg-emerald-50 text-emerald-700 border border-emerald-200' :
                  'bg-slate-100 text-slate-600 border border-slate-200'
                }`}>
                  {evt.category === 'exam' ? 'Kiểm tra' : evt.category === 'group' ? 'Học nhóm' : 'Bài tập'}
                </span>
              </div>
            ))}
          </div>
        </div>

        {/* Recent Flashcard Sets & Notes */}
        <div id="dashboard-recent-materials" className="bg-white rounded-2xl border border-slate-200 p-5 sm:p-6 shadow-2xs">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100">
            <div>
              <h3 className="text-base font-bold text-slate-900">Tài liệu & Thẻ học gần đây</h3>
              <p className="text-xs text-slate-500 mt-0.5">Ôn tập nhanh với 1 cú click</p>
            </div>
            <button
              onClick={() => onNavigate('flashcards')}
              className="text-xs text-indigo-600 hover:text-indigo-800 font-semibold flex items-center gap-1"
            >
              <span>Kho thẻ ({flashcardSets.length})</span>
              <ArrowRight className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="mt-4 space-y-3">
            {flashcardSets.slice(0, 3).map((set) => {
              const learnedCount = set.cards.filter(c => c.isLearned).length;
              const setPercent = Math.round((learnedCount / set.cards.length) * 100);
              const color = subjectColorMap[set.subject] || subjectColorMap['Toán'];

              return (
                <div
                  key={set.id}
                  className="p-3.5 rounded-xl border border-slate-200 hover:border-indigo-300 hover:shadow-xs transition-all flex items-center justify-between gap-3"
                >
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                      <span className={`text-[10px] font-bold px-2 py-0.5 rounded-md ${color.bg} ${color.text} border ${color.border}`}>
                        {set.subject}
                      </span>
                      <span className="text-[11px] text-slate-400 font-medium">{set.cards.length} thẻ ghi nhớ</span>
                    </div>
                    <h4 className="text-xs font-bold text-slate-900 truncate">{set.name}</h4>
                    <div className="flex items-center gap-2 mt-1.5">
                      <div className="w-28 bg-slate-100 h-1.5 rounded-full overflow-hidden">
                        <div className="bg-emerald-500 h-full rounded-full" style={{ width: `${setPercent}%` }} />
                      </div>
                      <span className="text-[10px] text-slate-500 font-medium">Đã thuộc {learnedCount}/{set.cards.length} ({setPercent}%)</span>
                    </div>
                  </div>

                  <button
                    onClick={() => onOpenStudySet(set.id)}
                    className="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs transition-colors shrink-0"
                  >
                    Học ngay
                  </button>
                </div>
              );
            })}
          </div>

          {/* Quick Note preview */}
          <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between">
            <span className="text-xs text-slate-500">Ghi chú gần nhất: <strong className="text-slate-800">{notes[0]?.title.slice(0, 30)}...</strong></span>
            <button
              onClick={() => onOpenNote(notes[0]?.id)}
              className="text-xs text-indigo-600 hover:text-indigo-800 font-semibold"
            >
              Mở đọc
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
