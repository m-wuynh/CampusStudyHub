import React, { useState } from 'react';
import { 
  User, 
  Mail, 
  School, 
  GraduationCap, 
  Target, 
  Bell, 
  Globe, 
  Sun, 
  Shield, 
  Save, 
  Check, 
  Sparkles,
  Smartphone
} from 'lucide-react';
import { UserProfile } from '../types';

interface SettingsViewProps {
  user: UserProfile;
  onUpdateUser: (updated: UserProfile) => void;
  showToast: (msg: string, type?: 'success' | 'error' | 'info') => void;
}

export const SettingsView: React.FC<SettingsViewProps> = ({
  user,
  onUpdateUser,
  showToast,
}) => {
  const [name, setName] = useState(user.name);
  const [email, setEmail] = useState(user.email);
  const [school, setSchool] = useState(user.school);
  const [grade, setGrade] = useState(user.grade);
  const [targetGpa, setTargetGpa] = useState<number>(user.targetGpa || 8.8);
  const [avatarUrl, setAvatarUrl] = useState(user.avatarUrl);

  // Preferences
  const [studyReminder, setStudyReminder] = useState(true);
  const [groupAnnouncementReminder, setGroupAnnouncementReminder] = useState(true);
  const [soundEnabled, setSoundEnabled] = useState(true);
  const [isSaving, setIsSaving] = useState(false);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setIsSaving(true);

    setTimeout(() => {
      onUpdateUser({
        ...user,
        name: name.trim(),
        email: email.trim(),
        school: school.trim(),
        grade: grade.trim(),
        targetGpa: targetGpa,
        avatarUrl: avatarUrl.trim(),
      });
      setIsSaving(false);
      showToast('Đã lưu cài đặt thông tin cá nhân thành công!', 'success');
    }, 400);
  };

  return (
    <div id="settings-view" className="max-w-4xl mx-auto space-y-6 pb-12">
      {/* Top Header */}
      <div className="bg-white rounded-2xl border border-slate-200 p-6 shadow-2xs">
        <div className="flex items-center gap-4">
          <img
            src={avatarUrl}
            alt={name}
            className="w-16 h-16 rounded-2xl object-cover border-2 border-indigo-200 shadow-2xs"
          />
          <div>
            <h2 className="text-lg font-bold text-slate-900 leading-tight">{name}</h2>
            <p className="text-xs text-slate-500 mt-0.5">{grade} • {school}</p>
            <div className="mt-2 flex items-center gap-2">
              <span className="text-[10px] font-bold px-2 py-0.5 bg-indigo-50 text-indigo-700 rounded-md border border-indigo-200">
                Tài khoản Học sinh
              </span>
              <span className="text-[10px] font-bold px-2 py-0.5 bg-emerald-50 text-emerald-700 rounded-md border border-emerald-200">
                Niên khóa 2024 - 2027
              </span>
            </div>
          </div>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        {/* Section 1: Personal Profile */}
        <div className="bg-white rounded-2xl border border-slate-200 p-6 shadow-2xs space-y-4">
          <div className="pb-3 border-b border-slate-100 flex items-center gap-2">
            <User className="w-4 h-4 text-indigo-600" />
            <h3 className="text-sm font-bold text-slate-900">Hồ sơ cá nhân (Student Profile)</h3>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Họ và tên học sinh</label>
              <input
                type="text"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none focus:bg-white focus:border-indigo-500"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Địa chỉ Email</label>
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none focus:bg-white focus:border-indigo-500"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Trường THPT đang theo học</label>
              <input
                type="text"
                required
                value={school}
                onChange={(e) => setSchool(e.target.value)}
                className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none focus:bg-white focus:border-indigo-500"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Lớp & Khối</label>
              <input
                type="text"
                required
                value={grade}
                onChange={(e) => setGrade(e.target.value)}
                className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none focus:bg-white focus:border-indigo-500"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Mục tiêu điểm trung bình (Target GPA)</label>
              <input
                type="number"
                step="0.1"
                min="5.0"
                max="10.0"
                value={targetGpa}
                onChange={(e) => setTargetGpa(parseFloat(e.target.value) || 8.0)}
                className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none focus:bg-white focus:border-indigo-500 font-mono"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Ảnh đại diện (URL)</label>
              <input
                type="text"
                value={avatarUrl}
                onChange={(e) => setAvatarUrl(e.target.value)}
                className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none focus:bg-white focus:border-indigo-500 font-mono"
              />
            </div>
          </div>
        </div>

        {/* Section 2: Preferences & Interface */}
        <div className="bg-white rounded-2xl border border-slate-200 p-6 shadow-2xs space-y-4">
          <div className="pb-3 border-b border-slate-100 flex items-center gap-2">
            <Sun className="w-4 h-4 text-indigo-600" />
            <h3 className="text-sm font-bold text-slate-900">Tùy chọn giao diện & Ngôn ngữ</h3>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Ngôn ngữ hiển thị</label>
              <div className="p-3 bg-slate-50 border border-slate-200 rounded-xl flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Globe className="w-4 h-4 text-slate-500" />
                  <span className="text-xs font-semibold text-slate-800">Tiếng Việt (Mặc định)</span>
                </div>
                <span className="text-[10px] font-bold text-emerald-700 bg-emerald-100 px-2 py-0.5 rounded">
                  Đang dùng
                </span>
              </div>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Giao diện màu sắc</label>
              <div className="p-3 bg-slate-50 border border-slate-200 rounded-xl flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Sun className="w-4 h-4 text-amber-500" />
                  <span className="text-xs font-semibold text-slate-800">Giao diện Sáng (Clean Light Mode)</span>
                </div>
                <span className="text-[10px] font-bold text-indigo-700 bg-indigo-100 px-2 py-0.5 rounded">
                  Chuẩn giáo dục
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* Section 3: Notifications & Reminders */}
        <div className="bg-white rounded-2xl border border-slate-200 p-6 shadow-2xs space-y-4">
          <div className="pb-3 border-b border-slate-100 flex items-center gap-2">
            <Bell className="w-4 h-4 text-indigo-600" />
            <h3 className="text-sm font-bold text-slate-900">Thông báo & Nhắc nhở kỷ luật</h3>
          </div>

          <div className="space-y-3">
            <div className="flex items-center justify-between p-3 rounded-xl border border-slate-200 hover:bg-slate-50 transition-colors">
              <div>
                <p className="text-xs font-bold text-slate-900">Nhắc nhở lịch học và bài tập hôm nay</p>
                <p className="text-[11px] text-slate-500">Nhận thông báo khi sắp đến giờ ôn tập hoặc hạn chót nộp bài</p>
              </div>
              <input
                type="checkbox"
                checked={studyReminder}
                onChange={(e) => setStudyReminder(e.target.checked)}
                className="w-4 h-4 text-indigo-600 rounded border-slate-300 focus:ring-indigo-500"
              />
            </div>

            <div className="flex items-center justify-between p-3 rounded-xl border border-slate-200 hover:bg-slate-50 transition-colors">
              <div>
                <p className="text-xs font-bold text-slate-900">Thông báo từ nhóm học tập</p>
                <p className="text-[11px] text-slate-500">Cập nhật khi có bài viết hoặc tài liệu mới được bạn cùng nhóm chia sẻ</p>
              </div>
              <input
                type="checkbox"
                checked={groupAnnouncementReminder}
                onChange={(e) => setGroupAnnouncementReminder(e.target.checked)}
                className="w-4 h-4 text-indigo-600 rounded border-slate-300 focus:ring-indigo-500"
              />
            </div>

            <div className="flex items-center justify-between p-3 rounded-xl border border-slate-200 hover:bg-slate-50 transition-colors">
              <div>
                <p className="text-xs font-bold text-slate-900">Âm thanh lật thẻ và hoàn thành mục tiêu</p>
                <p className="text-[11px] text-slate-500">Phát âm thanh nhẹ vui tai khi ghi nhớ thẻ hoặc hoàn thành nhiệm vụ</p>
              </div>
              <input
                type="checkbox"
                checked={soundEnabled}
                onChange={(e) => setSoundEnabled(e.target.checked)}
                className="w-4 h-4 text-indigo-600 rounded border-slate-300 focus:ring-indigo-500"
              />
            </div>
          </div>
        </div>

        {/* Action Button */}
        <div className="flex justify-end gap-3 pt-2">
          <button
            type="submit"
            disabled={isSaving}
            className="px-6 py-2.5 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white text-xs font-bold rounded-xl shadow-2xs transition-colors flex items-center gap-2"
          >
            {isSaving ? (
              <span>Đang lưu...</span>
            ) : (
              <>
                <Save className="w-4 h-4" />
                <span>Lưu tất cả thay đổi</span>
              </>
            )}
          </button>
        </div>
      </form>
    </div>
  );
};
