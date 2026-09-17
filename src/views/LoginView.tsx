import React, { useState } from 'react';
import { BookOpen, Check, Eye, EyeOff, Sparkles, ArrowRight, ShieldCheck } from 'lucide-react';
import { UserProfile } from '../types';

interface LoginViewProps {
  onLoginSuccess: (user: UserProfile) => void;
  showToast: (msg: string, type?: 'success' | 'error' | 'info') => void;
}

export const LoginView: React.FC<LoginViewProps> = ({ onLoginSuccess, showToast }) => {
  const [isRegisterMode, setIsRegisterMode] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  // Login form state
  const [loginEmail, setLoginEmail] = useState('minhanh.nguyen@thpt-hanoi.edu.vn');
  const [loginPassword, setLoginPassword] = useState('StudyHub@2026');
  const [rememberMe, setRememberMe] = useState(true);

  // Register form state
  const [regName, setRegName] = useState('');
  const [regEmail, setRegEmail] = useState('');
  const [regPassword, setRegPassword] = useState('');
  const [regConfirmPassword, setRegConfirmPassword] = useState('');
  const [regGrade, setRegGrade] = useState('Lớp 11');

  const handleLoginSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!loginEmail.trim() || !loginPassword.trim()) {
      showToast('Vui lòng điền đầy đủ email và mật khẩu.', 'error');
      return;
    }

    const demoUser: UserProfile = {
      id: 'u-101',
      name: loginEmail.includes('minhanh') ? 'Nguyễn Minh Anh' : 'Alex Nguyễn',
      email: loginEmail,
      grade: 'Lớp 11A1 (Chuyên Tự Nhiên)',
      school: 'Trường THPT Chuyên Hà Nội - Amsterdam',
      avatarUrl: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150&auto=format&fit=crop&q=80',
      studyStreakDays: 5,
    };

    showToast('Đăng nhập thành công! Chào mừng bạn quay lại.', 'success');
    onLoginSuccess(demoUser);
  };

  const handleRegisterSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!regName || !regEmail || !regPassword) {
      showToast('Vui lòng nhập đầy đủ các trường thông tin.', 'error');
      return;
    }
    if (regPassword !== regConfirmPassword) {
      showToast('Mật khẩu xác nhận không khớp.', 'error');
      return;
    }

    const newUser: UserProfile = {
      id: `u-${Date.now()}`,
      name: regName,
      email: regEmail,
      grade: `${regGrade}A1`,
      school: 'Trường THPT Amsterdam',
      avatarUrl: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=150&auto=format&fit=crop&q=80',
      studyStreakDays: 1,
    };

    showToast('Tạo tài khoản học tập thành công! Đang vào bảng điều khiển...', 'success');
    onLoginSuccess(newUser);
  };

  const handleDemoQuickLogin = () => {
    setLoginEmail('minhanh.nguyen@thpt-hanoi.edu.vn');
    setLoginPassword('StudyHub@2026');
    const demoUser: UserProfile = {
      id: 'u-101',
      name: 'Nguyễn Minh Anh',
      email: 'minhanh.nguyen@thpt-hanoi.edu.vn',
      grade: 'Lớp 11A1 (Chuyên Tự Nhiên)',
      school: 'Trường THPT Chuyên Hà Nội - Amsterdam',
      avatarUrl: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150&auto=format&fit=crop&q=80',
      studyStreakDays: 5,
    };
    showToast('Đã đăng nhập tài khoản học sinh mẫu (Nguyễn Minh Anh).', 'success');
    onLoginSuccess(demoUser);
  };

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col justify-center py-12 sm:px-6 lg:px-8">
      <div className="sm:mx-auto sm:w-full sm:max-w-md">
        {/* Brand Logo & Name */}
        <div className="flex justify-center items-center gap-3">
          <div className="w-12 h-12 rounded-2xl bg-indigo-600 flex items-center justify-center text-white shadow-md shadow-indigo-200">
            <BookOpen className="w-7 h-7" />
          </div>
          <div className="text-left">
            <h1 className="text-2xl font-black text-slate-900 tracking-tight">Study Hub</h1>
            <p className="text-xs text-indigo-600 font-semibold tracking-wide uppercase">Dành cho học sinh THPT</p>
          </div>
        </div>

        <h2 className="mt-6 text-center text-xl font-bold tracking-tight text-slate-900">
          {isRegisterMode ? 'Đăng ký tài khoản học sinh' : 'Chào mừng bạn trở lại!'}
        </h2>
        <p className="mt-1.5 text-center text-xs text-slate-500 max-w-xs mx-auto">
          {isRegisterMode
            ? 'Tham gia cùng hàng nghìn học sinh THPT tổ chức học tập khoa học và hiệu quả'
            : 'Đăng nhập để tiếp tục quản lý ghi chú, flashcard và thời khóa biểu của bạn'}
        </p>
      </div>

      <div className="mt-7 sm:mx-auto sm:w-full sm:max-w-md px-4">
        <div className="bg-white py-8 px-6 sm:px-10 rounded-2xl shadow-sm border border-slate-200">
          {!isRegisterMode ? (
            /* Login Form */
            <form id="login-form" onSubmit={handleLoginSubmit} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-slate-700">
                  Email / Tên đăng nhập
                </label>
                <div className="mt-1.5">
                  <input
                    id="login-email-input"
                    type="email"
                    required
                    value={loginEmail}
                    onChange={(e) => setLoginEmail(e.target.value)}
                    placeholder="vidu: minhanh.nguyen@thpt.edu.vn"
                    className="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-300 rounded-xl text-xs text-slate-900 placeholder-slate-400 focus:bg-white focus:border-indigo-500 focus:ring-2 focus:ring-indigo-100 outline-none transition-all"
                  />
                </div>
              </div>

              <div>
                <div className="flex items-center justify-between">
                  <label className="block text-xs font-semibold text-slate-700">
                    Mật khẩu
                  </label>
                  <button
                    type="button"
                    onClick={() => showToast('Gợi ý: Mật khẩu mặc định là StudyHub@2026', 'info')}
                    className="text-xs text-indigo-600 hover:text-indigo-800 font-medium"
                  >
                    Quên mật khẩu?
                  </button>
                </div>
                <div className="mt-1.5 relative">
                  <input
                    id="login-password-input"
                    type={showPassword ? 'text' : 'password'}
                    required
                    value={loginPassword}
                    onChange={(e) => setLoginPassword(e.target.value)}
                    placeholder="Nhập mật khẩu..."
                    className="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-300 rounded-xl text-xs text-slate-900 placeholder-slate-400 focus:bg-white focus:border-indigo-500 focus:ring-2 focus:ring-indigo-100 outline-none transition-all pr-10"
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600"
                  >
                    {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
              </div>

              <div className="flex items-center justify-between pt-1">
                <label className="flex items-center gap-2 cursor-pointer text-xs text-slate-600">
                  <input
                    id="remember-me-checkbox"
                    type="checkbox"
                    checked={rememberMe}
                    onChange={(e) => setRememberMe(e.target.checked)}
                    className="w-4 h-4 rounded text-indigo-600 border-slate-300 focus:ring-indigo-500"
                  />
                  <span>Ghi nhớ đăng nhập</span>
                </label>
              </div>

              <div className="pt-2 space-y-2.5">
                <button
                  id="login-submit-btn"
                  type="submit"
                  className="w-full py-2.5 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-semibold text-xs rounded-xl shadow-sm shadow-indigo-200 transition-colors flex items-center justify-center gap-2"
                >
                  <span>Đăng nhập</span>
                  <ArrowRight className="w-4 h-4" />
                </button>

                {/* Quick Demo Login helper */}
                <button
                  id="demo-login-quick-btn"
                  type="button"
                  onClick={handleDemoQuickLogin}
                  className="w-full py-2 px-3 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 font-semibold text-xs rounded-xl border border-indigo-200 transition-colors flex items-center justify-center gap-2"
                >
                  <Sparkles className="w-3.5 h-3.5 text-indigo-600" />
                  <span>Vào ngay với tài khoản mẫu (1-Click)</span>
                </button>
              </div>
            </form>
          ) : (
            /* Register Form */
            <form id="register-form" onSubmit={handleRegisterSubmit} className="space-y-3.5">
              <div>
                <label className="block text-xs font-semibold text-slate-700">Họ và tên</label>
                <input
                  id="reg-name-input"
                  type="text"
                  required
                  placeholder="Nguyễn Văn A"
                  value={regName}
                  onChange={(e) => setRegName(e.target.value)}
                  className="mt-1 w-full px-3.5 py-2 bg-slate-50 border border-slate-300 rounded-xl text-xs text-slate-900 focus:bg-white focus:border-indigo-500 focus:ring-2 focus:ring-indigo-100 outline-none"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700">Email học sinh</label>
                <input
                  id="reg-email-input"
                  type="email"
                  required
                  placeholder="hocsinh@thpt.edu.vn"
                  value={regEmail}
                  onChange={(e) => setRegEmail(e.target.value)}
                  className="mt-1 w-full px-3.5 py-2 bg-slate-50 border border-slate-300 rounded-xl text-xs text-slate-900 focus:bg-white focus:border-indigo-500 focus:ring-2 focus:ring-indigo-100 outline-none"
                />
              </div>

              <div className="grid grid-cols-2 gap-2.5">
                <div>
                  <label className="block text-xs font-semibold text-slate-700">Mật khẩu</label>
                  <input
                    id="reg-password-input"
                    type="password"
                    required
                    placeholder="••••••••"
                    value={regPassword}
                    onChange={(e) => setRegPassword(e.target.value)}
                    className="mt-1 w-full px-3 py-2 bg-slate-50 border border-slate-300 rounded-xl text-xs text-slate-900 focus:bg-white focus:border-indigo-500 focus:ring-2 focus:ring-indigo-100 outline-none"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-700">Nhập lại MK</label>
                  <input
                    id="reg-confirm-password-input"
                    type="password"
                    required
                    placeholder="••••••••"
                    value={regConfirmPassword}
                    onChange={(e) => setRegConfirmPassword(e.target.value)}
                    className="mt-1 w-full px-3 py-2 bg-slate-50 border border-slate-300 rounded-xl text-xs text-slate-900 focus:bg-white focus:border-indigo-500 focus:ring-2 focus:ring-indigo-100 outline-none"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700">Khối lớp</label>
                <select
                  id="reg-grade-select"
                  value={regGrade}
                  onChange={(e) => setRegGrade(e.target.value)}
                  className="mt-1 w-full px-3 py-2 bg-slate-50 border border-slate-300 rounded-xl text-xs text-slate-900 focus:bg-white focus:border-indigo-500 outline-none"
                >
                  <option value="Lớp 10">Lớp 10 (Khối 10)</option>
                  <option value="Lớp 11">Lớp 11 (Khối 11)</option>
                  <option value="Lớp 12">Lớp 12 (Khối 12 - Ôn thi THPTQG)</option>
                </select>
              </div>

              <div className="pt-2">
                <button
                  id="register-submit-btn"
                  type="submit"
                  className="w-full py-2.5 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-semibold text-xs rounded-xl shadow-sm transition-colors"
                >
                  Tạo tài khoản học tập
                </button>
              </div>
            </form>
          )}

          {/* Switch mode */}
          <div className="mt-6 pt-4 border-t border-slate-100 text-center">
            <p className="text-xs text-slate-600">
              {isRegisterMode ? 'Đã có tài khoản?' : 'Chưa có tài khoản?'}
              <button
                id="toggle-auth-mode-btn"
                type="button"
                onClick={() => setIsRegisterMode(!isRegisterMode)}
                className="ml-1.5 font-bold text-indigo-600 hover:text-indigo-800 transition-colors"
              >
                {isRegisterMode ? 'Đăng nhập ngay' : 'Đăng ký miễn phí'}
              </button>
            </p>
          </div>
        </div>

        {/* Realistic Blazor & ASP.NET Student architecture badge */}
        <div className="mt-6 text-center">
          <div className="inline-flex items-center gap-1.5 px-3 py-1 bg-white border border-slate-200 rounded-full text-[11px] text-slate-500 shadow-2xs">
            <ShieldCheck className="w-3.5 h-3.5 text-emerald-600" />
            <span>Mô hình nguyên mẫu giao diện cho ASP.NET Core & Blazor Web</span>
          </div>
        </div>
      </div>
    </div>
  );
};
