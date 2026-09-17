import React from 'react';
import { AlertCircle, LogOut, Trash2 } from 'lucide-react';

interface ConfirmDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  variant?: 'danger' | 'warning' | 'primary';
  iconType?: 'logout' | 'delete' | 'alert';
}

export const ConfirmDialog: React.FC<ConfirmDialogProps> = ({
  isOpen,
  onClose,
  onConfirm,
  title,
  message,
  confirmText = 'Xác nhận',
  cancelText = 'Hủy bỏ',
  variant = 'danger',
  iconType = 'alert',
}) => {
  if (!isOpen) return null;

  const getIcon = () => {
    switch (iconType) {
      case 'logout':
        return <LogOut className="w-6 h-6 text-amber-600" />;
      case 'delete':
        return <Trash2 className="w-6 h-6 text-rose-600" />;
      default:
        return <AlertCircle className="w-6 h-6 text-indigo-600" />;
    }
  };

  const getIconBg = () => {
    switch (iconType) {
      case 'logout':
        return 'bg-amber-100';
      case 'delete':
        return 'bg-rose-100';
      default:
        return 'bg-indigo-100';
    }
  };

  const getConfirmBtnClass = () => {
    switch (variant) {
      case 'danger':
        return 'bg-rose-600 hover:bg-rose-700 text-white focus:ring-rose-200';
      case 'warning':
        return 'bg-amber-600 hover:bg-amber-700 text-white focus:ring-amber-200';
      default:
        return 'bg-indigo-600 hover:bg-indigo-700 text-white focus:ring-indigo-200';
    }
  };

  return (
    <div 
      id="confirm-dialog-backdrop"
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div 
        id="confirm-dialog-card"
        className="w-full max-w-md bg-white rounded-2xl shadow-xl border border-slate-200 p-6 animate-in zoom-in-95 duration-150"
      >
        <div className="flex items-start gap-4">
          <div className={`p-3 rounded-xl shrink-0 ${getIconBg()}`}>
            {getIcon()}
          </div>
          <div className="flex-1">
            <h3 className="text-lg font-semibold text-slate-900">{title}</h3>
            <p className="text-sm text-slate-600 mt-1 leading-relaxed">{message}</p>
          </div>
        </div>

        <div className="mt-6 flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
          <button
            id="confirm-cancel-btn"
            type="button"
            onClick={onClose}
            className="px-4 py-2 text-sm font-medium text-slate-700 bg-white hover:bg-slate-100 border border-slate-300 rounded-xl transition-colors"
          >
            {cancelText}
          </button>
          <button
            id="confirm-action-btn"
            type="button"
            onClick={() => {
              onConfirm();
              onClose();
            }}
            className={`px-4 py-2 text-sm font-medium rounded-xl transition-colors focus:ring-4 ${getConfirmBtnClass()}`}
          >
            {confirmText}
          </button>
        </div>
      </div>
    </div>
  );
};
