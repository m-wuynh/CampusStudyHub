import React, { useState } from 'react';
import { 
  Award, 
  Target, 
  TrendingUp, 
  Plus, 
  Edit3, 
  Trash2, 
  CheckCircle2, 
  Circle, 
  Calendar, 
  FileSpreadsheet, 
  BarChart3, 
  AlertCircle,
  HelpCircle
} from 'lucide-react';
import { GradeRecord, AcademicGoal, Subject, UserProfile } from '../types';
import { subjectColorMap } from '../mockData';
import { Modal } from '../components/Modal';
import { ConfirmDialog } from '../components/ConfirmDialog';

interface GradesGoalsViewProps {
  user: UserProfile;
  grades: GradeRecord[];
  goals: AcademicGoal[];
  onSaveGrade: (grade: GradeRecord) => void;
  onDeleteGrade: (gradeId: string) => void;
  onSaveGoal: (goal: AcademicGoal) => void;
  onDeleteGoal: (goalId: string) => void;
  onToggleGoalComplete: (goalId: string) => void;
  showToast: (msg: string, type?: 'success' | 'error' | 'info') => void;
}

const subjectsList: Subject[] = ['Toán', 'Vật lý', 'Hóa học', 'Sinh học', 'Ngữ văn', 'Lịch sử', 'Địa lý', 'Tiếng Anh', 'GDCD', 'Tin học'];

export const GradesGoalsView: React.FC<GradesGoalsViewProps> = ({
  user,
  grades,
  goals,
  onSaveGrade,
  onDeleteGrade,
  onSaveGoal,
  onDeleteGoal,
  onToggleGoalComplete,
  showToast,
}) => {
  // Tabs: 'grades' vs 'goals'
  const [activeTab, setActiveTab] = useState<'grades' | 'goals'>('grades');

  // Grade Modal State
  const [isGradeModalOpen, setIsGradeModalOpen] = useState(false);
  const [editingGrade, setEditingGrade] = useState<GradeRecord | null>(null);
  const [gradeSubject, setGradeSubject] = useState<Subject>('Toán');
  const [gradeSemester, setGradeSemester] = useState<'Học kỳ 1' | 'Học kỳ 2'>('Học kỳ 1');
  const [gradeOral, setGradeOral] = useState<string>('8.5');
  const [grade15m, setGrade15m] = useState<string>('8.0, 9.0');
  const [grade1Period, setGrade1Period] = useState<string>('8.5');
  const [gradeMidterm, setGradeMidterm] = useState<string>('8.5');
  const [gradeFinal, setGradeFinal] = useState<string>('9.0');
  const [deleteGradeConfirmId, setDeleteGradeConfirmId] = useState<string | null>(null);

  // Goal Modal State
  const [isGoalModalOpen, setIsGoalModalOpen] = useState(false);
  const [editingGoal, setEditingGoal] = useState<AcademicGoal | null>(null);
  const [goalTitle, setGoalTitle] = useState('');
  const [goalSubject, setGoalSubject] = useState<Subject>('Toán');
  const [goalTargetDate, setGoalTargetDate] = useState('2026-09-30');
  const [goalProgress, setGoalProgress] = useState<number>(0);
  const [deleteGoalConfirmId, setDeleteGoalConfirmId] = useState<string | null>(null);

  // Calculate Overall GPA
  // In Vietnamese High School: Overall GPA is average of all subjects' averages
  const validGrades = grades.filter(g => g.averageScore > 0);
  const overallGpa = validGrades.length > 0
    ? (validGrades.reduce((sum, g) => sum + g.averageScore, 0) / validGrades.length).toFixed(1)
    : '8.4';
  const targetGpa = user.targetGpa || 8.8;

  // Determine classification
  const numGpa = parseFloat(overallGpa);
  let classification = 'Học sinh Giỏi';
  let badgeColor = 'bg-emerald-100 text-emerald-800 border-emerald-300';
  if (numGpa >= 9.0) {
    classification = 'Học sinh Xuất sắc';
    badgeColor = 'bg-indigo-100 text-indigo-800 border-indigo-300';
  } else if (numGpa >= 8.0) {
    classification = 'Học sinh Giỏi';
    badgeColor = 'bg-emerald-100 text-emerald-800 border-emerald-300';
  } else if (numGpa >= 6.5) {
    classification = 'Học sinh Khá';
    badgeColor = 'bg-blue-100 text-blue-800 border-blue-300';
  } else {
    classification = 'Học sinh Đạt';
    badgeColor = 'bg-amber-100 text-amber-800 border-amber-300';
  }

  // --- Grade Handlers ---
  const handleOpenAddGrade = () => {
    setEditingGrade(null);
    setGradeSubject('Toán');
    setGradeSemester('Học kỳ 1');
    setGradeOral('8.5');
    setGrade15m('8.0, 9.0');
    setGrade1Period('8.5');
    setGradeMidterm('8.5');
    setGradeFinal('9.0');
    setIsGradeModalOpen(true);
  };

  const handleOpenEditGrade = (rec: GradeRecord) => {
    setEditingGrade(rec);
    setGradeSubject(rec.subject);
    setGradeSemester(rec.semester);
    setGradeOral(rec.oralScores.join(', '));
    setGrade15m(rec.test15mScores.join(', '));
    setGrade1Period(rec.test1PeriodScores.join(', '));
    setGradeMidterm(rec.midtermScore !== undefined ? String(rec.midtermScore) : '');
    setGradeFinal(rec.finalScore !== undefined ? String(rec.finalScore) : '');
    setIsGradeModalOpen(true);
  };

  const parseScoreList = (str: string): number[] => {
    return str
      .split(',')
      .map(s => parseFloat(s.trim()))
      .filter(n => !isNaN(n) && n >= 0 && n <= 10);
  };

  const handleSaveGradeSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const oralArr = parseScoreList(gradeOral);
    const m15Arr = parseScoreList(grade15m);
    const m45Arr = parseScoreList(grade1Period);
    const mid = parseFloat(gradeMidterm);
    const fin = parseFloat(gradeFinal);

    // Calculate Vietnamese weighted subject average:
    // Regular scores (Oral + 15m) * 1
    // Midterm * 2
    // Final * 3
    // Total weight = regular count + 2 (for midterm) + 3 (for final)
    const regularScores = [...oralArr, ...m15Arr];
    let sum = regularScores.reduce((a, b) => a + b, 0);
    let totalWeight = regularScores.length;

    if (!isNaN(mid)) {
      sum += mid * 2;
      totalWeight += 2;
    }
    if (!isNaN(fin)) {
      sum += fin * 3;
      totalWeight += 3;
    }

    const avg = totalWeight > 0 ? parseFloat((sum / totalWeight).toFixed(1)) : 0;

    const newRecord: GradeRecord = {
      id: editingGrade ? editingGrade.id : `grd-${Date.now()}`,
      subject: gradeSubject,
      semester: gradeSemester,
      oralScores: oralArr,
      test15mScores: m15Arr,
      test1PeriodScores: m45Arr,
      midtermScore: !isNaN(mid) ? mid : undefined,
      finalScore: !isNaN(fin) ? fin : undefined,
      averageScore: avg,
    };

    onSaveGrade(newRecord);
    setIsGradeModalOpen(false);
    showToast(editingGrade ? 'Đã cập nhật bảng điểm môn học!' : 'Đã thêm điểm môn học mới!', 'success');
  };

  // --- Goal Handlers ---
  const handleOpenAddGoal = () => {
    setEditingGoal(null);
    setGoalTitle('');
    setGoalSubject('Toán');
    setGoalTargetDate('2026-09-30');
    setGoalProgress(0);
    setIsGoalModalOpen(true);
  };

  const handleOpenEditGoal = (g: AcademicGoal) => {
    setEditingGoal(g);
    setGoalTitle(g.title);
    setGoalSubject(g.subject);
    setGoalTargetDate(g.targetDate);
    setGoalProgress(g.progress);
    setIsGoalModalOpen(true);
  };

  const handleSaveGoalSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!goalTitle.trim()) {
      showToast('Vui lòng nhập mục tiêu học tập.', 'error');
      return;
    }

    const newGoal: AcademicGoal = {
      id: editingGoal ? editingGoal.id : `goal-${Date.now()}`,
      title: goalTitle.trim(),
      subject: goalSubject,
      targetDate: goalTargetDate,
      progress: Math.min(100, Math.max(0, goalProgress)),
      status: (goalProgress >= 100) ? 'Completed' : 'In progress',
    };

    onSaveGoal(newGoal);
    setIsGoalModalOpen(false);
    showToast(editingGoal ? 'Đã cập nhật mục tiêu học tập!' : 'Đã tạo mục tiêu mới!', 'success');
  };

  return (
    <div id="grades-goals-view" className="space-y-6 max-w-7xl mx-auto pb-12">
      {/* 1. Academic Overview (Part 1 as specified) */}
      <div id="academic-overview-cards" className="grid grid-cols-1 md:grid-cols-3 gap-5">
        {/* Card 1: Current GPA */}
        <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs flex items-center justify-between">
          <div>
            <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">
              Điểm trung bình (GPA)
            </span>
            <div className="flex items-baseline gap-2 mt-1">
              <span className="text-3xl font-extrabold text-indigo-600">{overallGpa}</span>
              <span className="text-xs font-semibold text-slate-400">/ 10.0</span>
            </div>
            <p className="text-[11px] text-slate-500 mt-1">
              Tính trên {validGrades.length} môn học đã có điểm
            </p>
          </div>
          <div className="w-12 h-12 rounded-2xl bg-indigo-50 text-indigo-600 flex items-center justify-center shrink-0">
            <Award className="w-6 h-6" />
          </div>
        </div>

        {/* Card 2: Target GPA */}
        <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs flex items-center justify-between">
          <div>
            <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">
              Mục tiêu học kỳ
            </span>
            <div className="flex items-baseline gap-2 mt-1">
              <span className="text-3xl font-extrabold text-emerald-600">{targetGpa.toFixed(1)}</span>
              <span className="text-xs font-semibold text-slate-400">/ 10.0</span>
            </div>
            <div className="flex items-center gap-1 text-[11px] text-emerald-700 mt-1 font-medium">
              <TrendingUp className="w-3.5 h-3.5" />
              <span>Cần tăng +{(targetGpa - parseFloat(overallGpa) > 0 ? (targetGpa - parseFloat(overallGpa)).toFixed(1) : '0.0')} để đạt mục tiêu</span>
            </div>
          </div>
          <div className="w-12 h-12 rounded-2xl bg-emerald-50 text-emerald-600 flex items-center justify-center shrink-0">
            <Target className="w-6 h-6" />
          </div>
        </div>

        {/* Card 3: Academic Standing */}
        <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs flex items-center justify-between">
          <div>
            <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">
              Xếp loại học lực
            </span>
            <div className="mt-1">
              <span className={`inline-block text-sm font-bold px-3 py-1 rounded-xl border ${badgeColor}`}>
                {classification}
              </span>
            </div>
            <p className="text-[11px] text-slate-500 mt-2">
              Hạnh kiểm: <strong className="text-emerald-700">Tốt</strong> • {user.school}
            </p>
          </div>
          <div className="w-12 h-12 rounded-2xl bg-amber-50 text-amber-600 flex items-center justify-center shrink-0">
            <BarChart3 className="w-6 h-6" />
          </div>
        </div>
      </div>

      {/* Navigation Tabs Bar: Grades vs Goals */}
      <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-2xs flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-4">
        <div className="flex items-center gap-2 bg-slate-100 p-1 rounded-xl">
          <button
            id="tab-grades-management"
            onClick={() => setActiveTab('grades')}
            className={`px-4 py-2 text-xs font-bold rounded-lg transition-all flex items-center gap-2 ${
              activeTab === 'grades'
                ? 'bg-white text-indigo-600 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <FileSpreadsheet className="w-3.5 h-3.5" />
            <span>Bảng điểm môn học (Grade Management)</span>
          </button>
          <button
            id="tab-goals-management"
            onClick={() => setActiveTab('goals')}
            className={`px-4 py-2 text-xs font-bold rounded-lg transition-all flex items-center gap-2 ${
              activeTab === 'goals'
                ? 'bg-white text-indigo-600 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <Target className="w-3.5 h-3.5" />
            <span>Mục tiêu học tập (Goals)</span>
          </button>
        </div>

        {activeTab === 'grades' ? (
          <button
            id="add-grade-record-btn"
            onClick={handleOpenAddGrade}
            className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs flex items-center justify-center gap-1.5 transition-colors shrink-0"
          >
            <Plus className="w-4 h-4" />
            <span>Thêm điểm môn học</span>
          </button>
        ) : (
          <button
            id="add-academic-goal-btn"
            onClick={handleOpenAddGoal}
            className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs flex items-center justify-center gap-1.5 transition-colors shrink-0"
          >
            <Plus className="w-4 h-4" />
            <span>Đặt mục tiêu mới</span>
          </button>
        )}
      </div>

      {/* Part 2: GRADE MANAGEMENT TABLE */}
      {activeTab === 'grades' && (
        <div id="grades-table-container" className="bg-white rounded-2xl border border-slate-200 shadow-2xs overflow-hidden">
          <div className="p-4 border-b border-slate-100 flex items-center justify-between">
            <div>
              <h3 className="text-sm font-bold text-slate-900">Bảng tổng hợp điểm Học kỳ 1 (Năm học 2026 - 2027)</h3>
              <p className="text-xs text-slate-500">
                Cách tính: Điểm thường xuyên (hệ số 1), Giữa kỳ (hệ số 2), Cuối kỳ (hệ số 3)
              </p>
            </div>
            <span className="text-xs font-bold px-2.5 py-1 bg-slate-100 text-slate-700 rounded-lg">
              {grades.length} môn học
            </span>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-slate-50 text-[11px] font-bold text-slate-500 uppercase tracking-wider border-b border-slate-200">
                  <th className="py-3 px-4">Môn học</th>
                  <th className="py-3 px-4">Điểm thường xuyên (Hệ số 1)</th>
                  <th className="py-3 px-4">Điểm Giữa kỳ (Hệ số 2)</th>
                  <th className="py-3 px-4">Điểm Cuối kỳ (Hệ số 3)</th>
                  <th className="py-3 px-4">ĐTB Môn</th>
                  <th className="py-3 px-4 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 text-xs text-slate-700">
                {grades.map((rec) => {
                  const color = subjectColorMap[rec.subject] || subjectColorMap['Toán'];
                  const regularFormatted = [...rec.oralScores, ...rec.test15mScores].join(' • ') || 'Chưa có';

                  return (
                    <tr key={rec.id} className="hover:bg-slate-50/80 transition-colors">
                      {/* Subject */}
                      <td className="py-3.5 px-4 font-bold text-slate-900">
                        <span className={`inline-block text-[10px] font-bold px-2 py-0.5 rounded-md mr-2 ${color.bg} ${color.text} border ${color.border}`}>
                          {rec.subject}
                        </span>
                      </td>

                      {/* Regular scores */}
                      <td className="py-3.5 px-4 font-mono font-medium text-slate-600">
                        {regularFormatted}
                      </td>

                      {/* Midterm score */}
                      <td className="py-3.5 px-4 font-mono font-bold text-slate-800">
                        {rec.midtermScore !== undefined ? rec.midtermScore.toFixed(1) : '-'}
                      </td>

                      {/* Final score */}
                      <td className="py-3.5 px-4 font-mono font-bold text-slate-800">
                        {rec.finalScore !== undefined ? rec.finalScore.toFixed(1) : '-'}
                      </td>

                      {/* Average */}
                      <td className="py-3.5 px-4">
                        <span className={`inline-block px-2.5 py-0.5 rounded-md font-mono font-extrabold ${
                          rec.averageScore >= 8.5
                            ? 'bg-emerald-100 text-emerald-800'
                            : rec.averageScore >= 6.5
                            ? 'bg-blue-100 text-blue-800'
                            : 'bg-amber-100 text-amber-800'
                        }`}>
                          {rec.averageScore.toFixed(1)}
                        </span>
                      </td>

                      {/* Actions: Edit, Delete */}
                      <td className="py-3.5 px-4 text-right">
                        <div className="flex items-center justify-end gap-1">
                          <button
                            onClick={() => handleOpenEditGrade(rec)}
                            className="p-1.5 text-slate-400 hover:text-indigo-600 hover:bg-indigo-50 rounded-lg transition-colors"
                            title="Sửa điểm"
                          >
                            <Edit3 className="w-3.5 h-3.5" />
                          </button>
                          <button
                            onClick={() => setDeleteGradeConfirmId(rec.id)}
                            className="p-1.5 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors"
                            title="Xóa môn này"
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Part 3: GOAL MANAGEMENT */}
      {activeTab === 'goals' && (
        <div id="goals-management-container" className="space-y-4">
          <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-2xs flex items-center justify-between">
            <div>
              <h3 className="text-sm font-bold text-slate-900">Danh sách mục tiêu học tập (Academic Goals)</h3>
              <p className="text-xs text-slate-500">Đặt mục tiêu điểm số và hoàn thành bài tập theo từng mốc thời gian</p>
            </div>
            <span className="text-xs font-bold text-emerald-700 bg-emerald-50 border border-emerald-200 px-3 py-1 rounded-xl">
              {goals.filter(g => g.status === 'Completed').length} / {goals.length} đã hoàn thành
            </span>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {goals.map((goal) => {
              const color = subjectColorMap[goal.subject] || subjectColorMap['Toán'];
              const isCompleted = goal.status === 'Completed';

              return (
                <div
                  key={goal.id}
                  id={`goal-item-${goal.id}`}
                  className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs hover:border-indigo-300 transition-all flex flex-col justify-between"
                >
                  <div>
                    {/* Header */}
                    <div className="flex items-center justify-between gap-2 mb-2">
                      <span className={`text-[10px] font-bold px-2 py-0.5 rounded-md ${color.bg} ${color.text} border ${color.border}`}>
                        {goal.subject}
                      </span>
                      <div className="flex items-center gap-1.5 text-xs text-slate-400">
                        <Calendar className="w-3.5 h-3.5" />
                        <span>Hạn chót: {goal.targetDate}</span>
                      </div>
                    </div>

                    <div className="flex items-start gap-3 mt-3">
                      <button
                        onClick={() => {
                          onToggleGoalComplete(goal.id);
                          showToast(isCompleted ? 'Đã chuyển mục tiêu về Đang thực hiện.' : 'Chúc mừng bạn đã hoàn thành mục tiêu!', 'success');
                        }}
                        className="mt-0.5 text-slate-400 hover:text-indigo-600 shrink-0"
                      >
                        {isCompleted ? (
                          <CheckCircle2 className="w-5 h-5 text-emerald-600 fill-emerald-100" />
                        ) : (
                          <Circle className="w-5 h-5 text-slate-400" />
                        )}
                      </button>

                      <div className="min-w-0 flex-1">
                        <h4 className={`text-sm font-bold leading-snug ${isCompleted ? 'line-through text-slate-400' : 'text-slate-900'}`}>
                          {goal.title}
                        </h4>

                        {/* Progress Bar */}
                        <div className="mt-3">
                          <div className="flex items-center justify-between text-[11px] font-semibold mb-1">
                            <span className="text-slate-500">Tiến độ thực hiện</span>
                            <span className={isCompleted ? 'text-emerald-600 font-bold' : 'text-indigo-600 font-bold'}>
                              {goal.progress}%
                            </span>
                          </div>
                          <div className="w-full bg-slate-100 h-2 rounded-full overflow-hidden">
                            <div
                              className={`h-full rounded-full transition-all duration-300 ${
                                isCompleted ? 'bg-emerald-500' : 'bg-indigo-600'
                              }`}
                              style={{ width: `${goal.progress}%` }}
                            />
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>

                  {/* Footer Actions */}
                  <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between">
                    <span className={`text-[10px] font-bold px-2 py-0.5 rounded-md ${
                      isCompleted ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700'
                    }`}>
                      {isCompleted ? 'Đã hoàn thành' : 'Đang tiến hành'}
                    </span>

                    <div className="flex items-center gap-1">
                      <button
                        onClick={() => handleOpenEditGoal(goal)}
                        className="p-1.5 text-slate-400 hover:text-indigo-600 hover:bg-slate-100 rounded-lg"
                        title="Chỉnh sửa mục tiêu"
                      >
                        <Edit3 className="w-3.5 h-3.5" />
                      </button>
                      <button
                        onClick={() => setDeleteGoalConfirmId(goal.id)}
                        className="p-1.5 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg"
                        title="Xóa mục tiêu"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Add / Edit Grade Modal */}
      <Modal
        isOpen={isGradeModalOpen}
        onClose={() => setIsGradeModalOpen(false)}
        title={editingGrade ? 'Chỉnh sửa điểm môn học' : 'Thêm kết quả học tập môn học'}
        subtitle="Hệ thống tự động tính điểm trung bình môn theo quy chế Bộ GD&ĐT"
      >
        <form onSubmit={handleSaveGradeSubmit} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Môn học</label>
              <select
                value={gradeSubject}
                onChange={(e) => setGradeSubject(e.target.value as Subject)}
                className="w-full px-3 py-2 text-xs text-slate-800 bg-slate-50 border border-slate-300 rounded-xl outline-none"
              >
                {subjectsList.map((sub) => (
                  <option key={sub} value={sub}>{sub}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Học kỳ</label>
              <select
                value={gradeSemester}
                onChange={(e) => setGradeSemester(e.target.value as 'Học kỳ 1' | 'Học kỳ 2')}
                className="w-full px-3 py-2 text-xs text-slate-800 bg-slate-50 border border-slate-300 rounded-xl outline-none"
              >
                <option value="Học kỳ 1">Học kỳ 1</option>
                <option value="Học kỳ 2">Học kỳ 2</option>
              </select>
            </div>
          </div>

          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">
              Điểm kiểm tra miệng & 15 phút (Hệ số 1, cách nhau bởi dấu phẩy)
            </label>
            <input
              type="text"
              placeholder="Ví dụ: 8.0, 8.5, 9.0"
              value={grade15m}
              onChange={(e) => setGrade15m(e.target.value)}
              className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none font-mono"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Điểm Giữa kỳ (Hệ số 2)
              </label>
              <input
                type="number"
                step="0.1"
                min="0"
                max="10"
                placeholder="Ví dụ: 8.5"
                value={gradeMidterm}
                onChange={(e) => setGradeMidterm(e.target.value)}
                className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none font-mono"
              />
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Điểm Cuối kỳ (Hệ số 3)
              </label>
              <input
                type="number"
                step="0.1"
                min="0"
                max="10"
                placeholder="Ví dụ: 9.0"
                value={gradeFinal}
                onChange={(e) => setGradeFinal(e.target.value)}
                className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none font-mono"
              />
            </div>
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
            <button
              type="button"
              onClick={() => setIsGradeModalOpen(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl"
            >
              Hủy
            </button>
            <button
              type="submit"
              className="px-4 py-2 text-xs font-semibold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl shadow-2xs"
            >
              {editingGrade ? 'Lưu điểm' : 'Thêm môn học'}
            </button>
          </div>
        </form>
      </Modal>

      {/* Add / Edit Goal Modal */}
      <Modal
        isOpen={isGoalModalOpen}
        onClose={() => setIsGoalModalOpen(false)}
        title={editingGoal ? 'Chỉnh sửa mục tiêu' : 'Đặt mục tiêu học tập mới'}
        subtitle="Cam kết rèn luyện và theo dõi tiến độ"
      >
        <form onSubmit={handleSaveGoalSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">Tên mục tiêu</label>
            <input
              type="text"
              required
              placeholder="Ví dụ: Đạt 9.0 Giữa kỳ môn Toán, Học 100 từ vựng Tiếng Anh..."
              value={goalTitle}
              onChange={(e) => setGoalTitle(e.target.value)}
              className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Môn học liên quan</label>
              <select
                value={goalSubject}
                onChange={(e) => setGoalSubject(e.target.value as Subject)}
                className="w-full px-3 py-2 text-xs text-slate-800 bg-slate-50 border border-slate-300 rounded-xl outline-none"
              >
                {subjectsList.map((sub) => (
                  <option key={sub} value={sub}>{sub}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Hạn hoàn thành</label>
              <input
                type="date"
                required
                value={goalTargetDate}
                onChange={(e) => setGoalTargetDate(e.target.value)}
                className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none"
              />
            </div>
          </div>

          <div>
            <div className="flex items-center justify-between mb-1">
              <label className="block text-xs font-bold text-slate-700">Tiến độ hiện tại (%)</label>
              <span className="text-xs font-mono font-bold text-indigo-600">{goalProgress}%</span>
            </div>
            <input
              type="range"
              min="0"
              max="100"
              step="5"
              value={goalProgress}
              onChange={(e) => setGoalProgress(parseInt(e.target.value, 10))}
              className="w-full accent-indigo-600"
            />
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
            <button
              type="button"
              onClick={() => setIsGoalModalOpen(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl"
            >
              Hủy
            </button>
            <button
              type="submit"
              className="px-4 py-2 text-xs font-semibold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl shadow-2xs"
            >
              {editingGoal ? 'Lưu cập nhật' : 'Tạo mục tiêu'}
            </button>
          </div>
        </form>
      </Modal>

      {/* Delete Grade Confirmation */}
      <ConfirmDialog
        isOpen={!!deleteGradeConfirmId}
        onClose={() => setDeleteGradeConfirmId(null)}
        onConfirm={() => {
          if (deleteGradeConfirmId) {
            onDeleteGrade(deleteGradeConfirmId);
            showToast('Đã xóa điểm môn học.', 'info');
            setDeleteGradeConfirmId(null);
          }
        }}
        title="Xóa điểm môn học này?"
        message="Điểm của môn này sẽ bị xóa khỏi bảng tổng hợp điểm học kỳ."
        confirmText="Xóa môn"
        cancelText="Hủy"
        variant="danger"
        iconType="delete"
      />

      {/* Delete Goal Confirmation */}
      <ConfirmDialog
        isOpen={!!deleteGoalConfirmId}
        onClose={() => setDeleteGoalConfirmId(null)}
        onConfirm={() => {
          if (deleteGoalConfirmId) {
            onDeleteGoal(deleteGoalConfirmId);
            showToast('Đã xóa mục tiêu học tập.', 'info');
            setDeleteGoalConfirmId(null);
          }
        }}
        title="Xóa mục tiêu này?"
        message="Mục tiêu này sẽ bị xóa khỏi danh sách theo dõi."
        confirmText="Xóa mục tiêu"
        cancelText="Hủy"
        variant="danger"
        iconType="delete"
      />
    </div>
  );
};
