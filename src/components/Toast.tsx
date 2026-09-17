import React from 'react';
import { CheckCircle2, AlertCircle, Info, X } from 'lucide-react';

export interface ToastMessage {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
}

interface ToastContainerProps {
  toasts: ToastMessage[];
  onDismiss?: (id: string) => void;
  onCloseToast?: (id: string) => void;
}

export const ToastContainer: React.FC<ToastContainerProps> = ({ toasts, onDismiss, onCloseToast }) => {
  if (toasts.length === 0) return null;

  const handleClose = (id: string) => {
    if (onCloseToast) onCloseToast(id);
    else if (onDismiss) onDismiss(id);
  };

  return (
    <div id="toast-container" className="fixed bottom-5 right-5 z-50 flex flex-col gap-2 pointer-events-none max-w-sm w-full">
      {toasts.map((t) => (
        <div
          key={t.id}
          id={`toast-${t.id}`}
          className={`pointer-events-auto flex items-center justify-between gap-3 px-4 py-3 rounded-xl shadow-lg border text-sm transition-all duration-200 animate-in slide-in-from-bottom-2 ${
            t.type === 'success'
              ? 'bg-emerald-50 text-emerald-900 border-emerald-200'
              : t.type === 'error'
              ? 'bg-rose-50 text-rose-900 border-rose-200'
              : 'bg-indigo-50 text-indigo-900 border-indigo-200'
          }`}
        >
          <div className="flex items-center gap-2.5">
            {t.type === 'success' && <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0" />}
            {t.type === 'error' && <AlertCircle className="w-5 h-5 text-rose-600 shrink-0" />}
            {t.type === 'info' && <Info className="w-5 h-5 text-indigo-600 shrink-0" />}
            <span className="font-medium text-slate-800">{t.message}</span>
          </div>
          <button
            onClick={() => handleClose(t.id)}
            className="p-1 hover:bg-black/5 rounded text-slate-500 hover:text-slate-800 transition-colors"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      ))}
    </div>
  );
};

export const Toast = ToastContainer;
