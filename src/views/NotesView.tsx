import React, { useState } from 'react';
import { 
  FileText, 
  Plus, 
  Search, 
  Pin, 
  Trash2, 
  Edit3, 
  Save, 
  X, 
  Bold, 
  Italic, 
  Underline, 
  Heading1, 
  Heading2, 
  List, 
  ListOrdered, 
  Tag, 
  Calendar,
  BookOpen,
  ArrowLeft
} from 'lucide-react';
import { Note, Subject } from '../types';
import { subjectColorMap } from '../mockData';
import { ConfirmDialog } from '../components/ConfirmDialog';

interface NotesViewProps {
  notes: Note[];
  onSaveNote: (note: Note) => void;
  onDeleteNote: (noteId: string) => void;
  showToast: (msg: string, type?: 'success' | 'error' | 'info') => void;
  activeNoteId?: string | null;
  onClearActiveNote?: () => void;
}

const subjectsList: Subject[] = ['Toán', 'Vật lý', 'Hóa học', 'Ngữ văn', 'Tiếng Anh', 'Sinh học', 'Lịch sử', 'Tin học'];

export const NotesView: React.FC<NotesViewProps> = ({
  notes,
  onSaveNote,
  onDeleteNote,
  showToast,
  activeNoteId,
  onClearActiveNote,
}) => {
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedSubject, setSelectedSubject] = useState<string>('all');
  
  // Selected note for viewing / editing
  const [selectedNote, setSelectedNote] = useState<Note | null>(() => {
    if (activeNoteId) {
      return notes.find(n => n.id === activeNoteId) || null;
    }
    return notes[0] || null;
  });

  const [isEditing, setIsEditing] = useState(false);
  const [isCreatingNew, setIsCreatingNew] = useState(false);

  // Edit form state
  const [editTitle, setEditTitle] = useState('');
  const [editSubject, setEditSubject] = useState<Subject>('Toán');
  const [editContent, setEditContent] = useState('');
  const [editTags, setEditTags] = useState('');
  const [editPinned, setEditPinned] = useState(false);

  // Delete dialog
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);

  // Filter notes
  const filteredNotes = notes.filter((n) => {
    const matchSubject = selectedSubject === 'all' || n.subject === selectedSubject;
    const matchSearch = n.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      n.content.toLowerCase().includes(searchQuery.toLowerCase()) ||
      n.tags?.some(t => t.toLowerCase().includes(searchQuery.toLowerCase()));
    return matchSubject && matchSearch;
  });

  const handleStartCreate = () => {
    setIsCreatingNew(true);
    setIsEditing(true);
    setEditTitle('');
    setEditSubject('Vật lý');
    setEditContent('');
    setEditTags('Lý thuyết, Ghi nhớ');
    setEditPinned(false);
    if (onClearActiveNote) onClearActiveNote();
  };

  const handleStartEdit = (note: Note) => {
    setIsCreatingNew(false);
    setIsEditing(true);
    setEditTitle(note.title);
    setEditSubject(note.subject);
    setEditContent(note.content);
    setEditTags(note.tags?.join(', ') || '');
    setEditPinned(!!note.isPinned);
  };

  const handleCancelEdit = () => {
    setIsEditing(false);
    setIsCreatingNew(false);
  };

  const handleSave = () => {
    if (!editTitle.trim()) {
      showToast('Vui lòng nhập tiêu đề cho ghi chú.', 'error');
      return;
    }

    const tagArray = editTags
      .split(',')
      .map(t => t.trim())
      .filter(t => t.length > 0);

    const updatedNote: Note = {
      id: isCreatingNew ? `note-${Date.now()}` : (selectedNote?.id || `note-${Date.now()}`),
      title: editTitle.trim(),
      subject: editSubject,
      content: editContent,
      updatedAt: new Date().toISOString(),
      isPinned: editPinned,
      tags: tagArray,
    };

    onSaveNote(updatedNote);
    setSelectedNote(updatedNote);
    setIsEditing(false);
    setIsCreatingNew(false);
    showToast(isCreatingNew ? 'Đã tạo ghi chú mới!' : 'Đã cập nhật ghi chú thành công!', 'success');
  };

  const handleDeletePrompt = (noteId: string) => {
    setDeleteConfirmId(noteId);
  };

  const handleConfirmDelete = () => {
    if (deleteConfirmId) {
      onDeleteNote(deleteConfirmId);
      if (selectedNote?.id === deleteConfirmId) {
        const remaining = notes.filter(n => n.id !== deleteConfirmId);
        setSelectedNote(remaining[0] || null);
      }
      showToast('Đã xóa ghi chú.', 'info');
      setDeleteConfirmId(null);
    }
  };

  // Helper formatting insert
  const applyFormatting = (prefix: string, suffix: string = '') => {
    const textarea = document.getElementById('note-editor-textarea') as HTMLTextAreaElement;
    if (!textarea) return;

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const previousText = textarea.value;
    const selectedText = previousText.substring(start, end) || 'Văn bản mẫu';

    const replacement = `${prefix}${selectedText}${suffix}`;
    const newContent = previousText.substring(0, start) + replacement + previousText.substring(end);
    setEditContent(newContent);

    setTimeout(() => {
      textarea.focus();
      textarea.setSelectionRange(start + prefix.length, start + prefix.length + selectedText.length);
    }, 10);
  };

  return (
    <div id="notes-view" className="h-[calc(100vh-6.5rem)] flex flex-col max-w-7xl mx-auto pb-4">
      {/* Top filter bar */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3 mb-4 bg-white p-4 rounded-2xl border border-slate-200 shadow-2xs">
        <div className="flex items-center gap-2 overflow-x-auto pb-1 sm:pb-0">
          <button
            id="filter-subject-all"
            onClick={() => setSelectedSubject('all')}
            className={`px-3 py-1.5 rounded-xl text-xs font-semibold whitespace-nowrap transition-colors ${
              selectedSubject === 'all'
                ? 'bg-indigo-600 text-white shadow-2xs'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            Tất cả môn ({notes.length})
          </button>
          {subjectsList.map((sub) => {
            const count = notes.filter(n => n.subject === sub).length;
            const isSelected = selectedSubject === sub;
            return (
              <button
                key={sub}
                onClick={() => setSelectedSubject(sub)}
                className={`px-3 py-1.5 rounded-xl text-xs font-semibold whitespace-nowrap transition-colors flex items-center gap-1.5 ${
                  isSelected
                    ? 'bg-indigo-600 text-white shadow-2xs'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                }`}
              >
                <span>{sub}</span>
                {count > 0 && (
                  <span className={`px-1.5 py-0.2 rounded-full text-[10px] ${
                    isSelected ? 'bg-indigo-700 text-white' : 'bg-white text-slate-600'
                  }`}>
                    {count}
                  </span>
                )}
              </button>
            );
          })}
        </div>

        <div className="flex items-center gap-2">
          <div className="relative flex-1 sm:w-60">
            <Search className="w-3.5 h-3.5 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none" />
            <input
              id="notes-search-input"
              type="text"
              placeholder="Tìm kiếm bài ghi..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-8 pr-3 py-1.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-800 placeholder-slate-400 focus:bg-white focus:border-indigo-400 outline-none"
            />
          </div>
          <button
            id="create-note-btn"
            onClick={handleStartCreate}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs transition-colors shrink-0"
          >
            <Plus className="w-4 h-4" />
            <span>Tạo ghi chú</span>
          </button>
        </div>
      </div>

      {/* Main split view: Notes List (Left) and Note Viewer/Editor (Right) */}
      <div className="flex-1 grid grid-cols-1 lg:grid-cols-12 gap-4 min-h-0">
        {/* Left Column: Notes List (4 cols) */}
        <div className={`lg:col-span-4 bg-white rounded-2xl border border-slate-200 shadow-2xs flex flex-col min-h-0 overflow-hidden ${
          isEditing ? 'hidden lg:flex' : 'flex'
        }`}>
          <div className="p-3.5 border-b border-slate-100 bg-slate-50/50 flex items-center justify-between">
            <span className="text-xs font-bold text-slate-600 uppercase tracking-wider">
              Danh sách ghi chú ({filteredNotes.length})
            </span>
            <span className="text-[11px] text-slate-400">Tự động lưu</span>
          </div>

          <div className="flex-1 overflow-y-auto divide-y divide-slate-100 p-2 space-y-1">
            {filteredNotes.length === 0 ? (
              <div className="p-8 text-center">
                <FileText className="w-10 h-10 text-slate-300 mx-auto mb-2" />
                <p className="text-xs font-semibold text-slate-600">Không tìm thấy ghi chú nào</p>
                <p className="text-[11px] text-slate-400 mt-1">Hãy thử tìm từ khóa khác hoặc tạo ghi chú mới.</p>
              </div>
            ) : (
              filteredNotes.map((note) => {
                const isCurrent = selectedNote?.id === note.id && !isCreatingNew;
                const color = subjectColorMap[note.subject] || subjectColorMap['Toán'];
                return (
                  <div
                    key={note.id}
                    id={`note-card-${note.id}`}
                    onClick={() => {
                      setSelectedNote(note);
                      setIsEditing(false);
                      setIsCreatingNew(false);
                    }}
                    className={`p-3 rounded-xl cursor-pointer transition-all ${
                      isCurrent
                        ? 'bg-indigo-50/80 border-indigo-200 shadow-2xs'
                        : 'hover:bg-slate-50'
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="flex items-center gap-1.5 mb-1">
                        <span className={`text-[10px] font-bold px-2 py-0.5 rounded-md ${color.bg} ${color.text} border ${color.border}`}>
                          {note.subject}
                        </span>
                        {note.isPinned && (
                          <span className="text-amber-600 flex items-center text-[10px] font-semibold">
                            <Pin className="w-3 h-3 fill-amber-500" />
                          </span>
                        )}
                      </div>
                      <span className="text-[10px] text-slate-400 font-mono">
                        {note.updatedAt.slice(5, 10)}
                      </span>
                    </div>

                    <h4 className={`text-xs font-bold line-clamp-1 ${isCurrent ? 'text-indigo-900' : 'text-slate-800'}`}>
                      {note.title}
                    </h4>

                    <p className="text-[11px] text-slate-500 line-clamp-2 mt-1 leading-snug">
                      {note.content.replace(/[#*$`]/g, '')}
                    </p>

                    {note.tags && note.tags.length > 0 && (
                      <div className="flex flex-wrap gap-1 mt-2">
                        {note.tags.slice(0, 3).map((t, idx) => (
                          <span key={idx} className="text-[9px] bg-white border border-slate-200 text-slate-500 px-1.5 py-0.5 rounded">
                            #{t}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                );
              })
            )}
          </div>
        </div>

        {/* Right Column: Note Viewer OR Note Editor (8 cols) */}
        <div className="lg:col-span-8 bg-white rounded-2xl border border-slate-200 shadow-2xs flex flex-col min-h-0 overflow-hidden">
          {isEditing ? (
            /* Note Editor Form */
            <div className="flex-1 flex flex-col min-h-0">
              {/* Editor Header */}
              <div className="p-4 border-b border-slate-100 bg-slate-50/50 flex flex-wrap items-center justify-between gap-3">
                <div className="flex items-center gap-2">
                  <button
                    onClick={handleCancelEdit}
                    className="p-1.5 text-slate-500 hover:text-slate-800 hover:bg-slate-200 rounded-lg lg:hidden"
                    title="Quay lại"
                  >
                    <ArrowLeft className="w-4 h-4" />
                  </button>
                  <h3 className="text-sm font-bold text-slate-900">
                    {isCreatingNew ? 'Tạo ghi chú mới' : 'Chỉnh sửa ghi chú'}
                  </h3>
                </div>

                <div className="flex items-center gap-2">
                  <button
                    id="note-cancel-btn"
                    onClick={handleCancelEdit}
                    className="px-3 py-1.5 text-xs font-semibold text-slate-600 bg-white hover:bg-slate-100 border border-slate-200 rounded-xl transition-colors"
                  >
                    Hủy
                  </button>
                  <button
                    id="note-save-btn"
                    onClick={handleSave}
                    className="px-4 py-1.5 text-xs font-semibold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl shadow-2xs flex items-center gap-1.5 transition-colors"
                  >
                    <Save className="w-3.5 h-3.5" />
                    <span>Lưu ghi chú</span>
                  </button>
                </div>
              </div>

              {/* Editor Inputs */}
              <div className="p-4 space-y-3 flex-1 flex flex-col min-h-0 overflow-y-auto">
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  <div className="sm:col-span-2">
                    <label className="block text-[11px] font-bold text-slate-500 uppercase tracking-wider mb-1">
                      Tiêu đề ghi chú
                    </label>
                    <input
                      id="note-edit-title"
                      type="text"
                      placeholder="Ví dụ: Định luật Newton, Công thức lượng giác..."
                      value={editTitle}
                      onChange={(e) => setEditTitle(e.target.value)}
                      className="w-full px-3.5 py-2 text-sm font-bold text-slate-900 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:border-indigo-400 outline-none"
                    />
                  </div>

                  <div>
                    <label className="block text-[11px] font-bold text-slate-500 uppercase tracking-wider mb-1">
                      Môn học
                    </label>
                    <select
                      id="note-edit-subject"
                      value={editSubject}
                      onChange={(e) => setEditSubject(e.target.value as Subject)}
                      className="w-full px-3 py-2 text-xs font-semibold text-slate-800 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:border-indigo-400 outline-none"
                    >
                      {subjectsList.map((sub) => (
                        <option key={sub} value={sub}>{sub}</option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Formatting Toolbar */}
                <div className="p-2 bg-slate-50 border border-slate-200 rounded-xl flex flex-wrap items-center gap-1">
                  <button
                    type="button"
                    onClick={() => applyFormatting('**', '**')}
                    className="p-1.5 text-slate-600 hover:text-indigo-600 hover:bg-white rounded-lg text-xs font-bold"
                    title="In đậm (Bold)"
                  >
                    <Bold className="w-3.5 h-3.5" />
                  </button>
                  <button
                    type="button"
                    onClick={() => applyFormatting('*', '*')}
                    className="p-1.5 text-slate-600 hover:text-indigo-600 hover:bg-white rounded-lg text-xs"
                    title="In nghiêng (Italic)"
                  >
                    <Italic className="w-3.5 h-3.5" />
                  </button>
                  <button
                    type="button"
                    onClick={() => applyFormatting('<u>', '</u>')}
                    className="p-1.5 text-slate-600 hover:text-indigo-600 hover:bg-white rounded-lg text-xs"
                    title="Gạch chân (Underline)"
                  >
                    <Underline className="w-3.5 h-3.5" />
                  </button>
                  <div className="h-4 w-[1px] bg-slate-200 mx-1" />
                  <button
                    type="button"
                    onClick={() => applyFormatting('### ')}
                    className="p-1.5 text-slate-600 hover:text-indigo-600 hover:bg-white rounded-lg text-xs font-bold"
                    title="Tiêu đề H1"
                  >
                    <Heading1 className="w-3.5 h-3.5" />
                  </button>
                  <button
                    type="button"
                    onClick={() => applyFormatting('#### ')}
                    className="p-1.5 text-slate-600 hover:text-indigo-600 hover:bg-white rounded-lg text-xs font-bold"
                    title="Tiêu đề H2"
                  >
                    <Heading2 className="w-3.5 h-3.5" />
                  </button>
                  <div className="h-4 w-[1px] bg-slate-200 mx-1" />
                  <button
                    type="button"
                    onClick={() => applyFormatting('- ')}
                    className="p-1.5 text-slate-600 hover:text-indigo-600 hover:bg-white rounded-lg text-xs"
                    title="Danh sách gạch đầu dòng"
                  >
                    <List className="w-3.5 h-3.5" />
                  </button>
                  <button
                    type="button"
                    onClick={() => applyFormatting('1. ')}
                    className="p-1.5 text-slate-600 hover:text-indigo-600 hover:bg-white rounded-lg text-xs"
                    title="Danh sách đánh số"
                  >
                    <ListOrdered className="w-3.5 h-3.5" />
                  </button>
                  <div className="h-4 w-[1px] bg-slate-200 mx-1" />
                  <button
                    type="button"
                    onClick={() => setEditPinned(!editPinned)}
                    className={`px-2 py-1 rounded-lg text-xs flex items-center gap-1 font-semibold ${
                      editPinned ? 'bg-amber-100 text-amber-800' : 'text-slate-600 hover:bg-white'
                    }`}
                  >
                    <Pin className="w-3 h-3" />
                    <span>Ghim lên đầu</span>
                  </button>
                </div>

                {/* Content Textarea */}
                <div className="flex-1 flex flex-col min-h-0">
                  <label className="block text-[11px] font-bold text-slate-500 uppercase tracking-wider mb-1">
                    Nội dung bài học
                  </label>
                  <textarea
                    id="note-editor-textarea"
                    value={editContent}
                    onChange={(e) => setEditContent(e.target.value)}
                    placeholder="Viết nội dung bài học tại đây... Hỗ trợ định dạng tiêu đề, danh sách, công thức và ghi chú quan trọng."
                    className="w-full flex-1 min-h-[220px] p-3.5 text-xs text-slate-800 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:border-indigo-400 outline-none font-mono resize-none leading-relaxed"
                  />
                </div>

                {/* Tags input */}
                <div>
                  <label className="block text-[11px] font-bold text-slate-500 uppercase tracking-wider mb-1">
                    Thẻ từ khóa (phân cách bằng dấu phẩy)
                  </label>
                  <input
                    id="note-edit-tags"
                    type="text"
                    placeholder="Ví dụ: Ôn thi giữa kỳ, Công thức, Động lực học"
                    value={editTags}
                    onChange={(e) => setEditTags(e.target.value)}
                    className="w-full px-3 py-1.5 text-xs text-slate-800 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:border-indigo-400 outline-none"
                  />
                </div>
              </div>
            </div>
          ) : selectedNote ? (
            /* Note Detail Viewer */
            <div className="flex-1 flex flex-col min-h-0">
              {/* Viewer Header */}
              <div className="p-4 sm:p-5 border-b border-slate-100 bg-slate-50/50 flex items-center justify-between gap-3">
                <div className="min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className={`text-[10px] font-bold px-2 py-0.5 rounded-md ${
                      subjectColorMap[selectedNote.subject]?.bg || 'bg-slate-100'
                    } ${subjectColorMap[selectedNote.subject]?.text || 'text-slate-800'} border ${
                      subjectColorMap[selectedNote.subject]?.border || 'border-slate-200'
                    }`}>
                      {selectedNote.subject}
                    </span>
                    <span className="text-xs text-slate-400 flex items-center gap-1">
                      <Calendar className="w-3 h-3" />
                      Cập nhật: {new Date(selectedNote.updatedAt).toLocaleDateString('vi-VN')}
                    </span>
                  </div>
                  <h2 className="text-base sm:text-lg font-bold text-slate-900 leading-tight">
                    {selectedNote.title}
                  </h2>
                </div>

                <div className="flex items-center gap-2 shrink-0">
                  <button
                    id="note-edit-btn"
                    onClick={() => handleStartEdit(selectedNote)}
                    className="flex items-center gap-1.5 px-3 py-1.5 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 text-xs font-semibold rounded-xl border border-indigo-200 transition-colors"
                  >
                    <Edit3 className="w-3.5 h-3.5" />
                    <span>Chỉnh sửa</span>
                  </button>
                  <button
                    id="note-delete-btn"
                    onClick={() => handleDeletePrompt(selectedNote.id)}
                    className="p-2 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-xl transition-colors"
                    title="Xóa ghi chú"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              </div>

              {/* Note Content display */}
              <div className="p-6 flex-1 overflow-y-auto prose prose-slate max-w-none text-xs leading-relaxed">
                <div className="whitespace-pre-wrap font-sans text-slate-800 leading-relaxed text-[13px] space-y-3">
                  {selectedNote.content.split('\n\n').map((paragraph, idx) => {
                    if (paragraph.startsWith('### ')) {
                      return (
                        <h3 key={idx} className="text-sm font-bold text-slate-900 pt-2 pb-1 border-b border-slate-100">
                          {paragraph.replace('### ', '')}
                        </h3>
                      );
                    }
                    if (paragraph.startsWith('#### ')) {
                      return (
                        <h4 key={idx} className="text-xs font-bold text-slate-800 pt-1">
                          {paragraph.replace('#### ', '')}
                        </h4>
                      );
                    }
                    if (paragraph.startsWith('- ') || paragraph.startsWith('1. ')) {
                      return (
                        <div key={idx} className="pl-4 space-y-1 text-slate-700">
                          {paragraph.split('\n').map((item, itemIdx) => (
                            <p key={itemIdx} className="list-item">
                              {item.replace(/^[-*•]\s+/, '').replace(/^\d+\.\s+/, '')}
                            </p>
                          ))}
                        </div>
                      );
                    }
                    return (
                      <p key={idx} className="text-slate-700 leading-relaxed">
                        {paragraph}
                      </p>
                    );
                  })}
                </div>

                {selectedNote.tags && selectedNote.tags.length > 0 && (
                  <div className="mt-8 pt-4 border-t border-slate-100 flex items-center gap-2 flex-wrap">
                    <span className="text-[11px] font-semibold text-slate-400 flex items-center gap-1">
                      <Tag className="w-3 h-3" /> Thẻ liên quan:
                    </span>
                    {selectedNote.tags.map((t, idx) => (
                      <span key={idx} className="px-2 py-0.5 bg-slate-100 text-slate-600 rounded-md text-[11px] font-medium">
                        #{t}
                      </span>
                    ))}
                  </div>
                )}
              </div>
            </div>
          ) : (
            <div className="flex-1 flex flex-col items-center justify-center p-8 text-center">
              <BookOpen className="w-12 h-12 text-slate-300 mb-3" />
              <h3 className="text-sm font-bold text-slate-800">Chưa chọn ghi chú nào</h3>
              <p className="text-xs text-slate-500 mt-1 max-w-sm">
                Hãy chọn một ghi chú từ danh sách bên trái hoặc tạo ghi chú mới để bắt đầu học.
              </p>
              <button
                onClick={handleStartCreate}
                className="mt-4 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs transition-colors flex items-center gap-1.5"
              >
                <Plus className="w-4 h-4" />
                <span>Tạo ghi chú đầu tiên</span>
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Confirmation Dialog for Delete */}
      <ConfirmDialog
        isOpen={!!deleteConfirmId}
        onClose={() => setDeleteConfirmId(null)}
        onConfirm={handleConfirmDelete}
        title="Xóa bài ghi chú này?"
        message="Ghi chú sẽ bị xóa vĩnh viễn khỏi danh sách học tập của bạn. Thao tác này không thể hoàn tác."
        confirmText="Xóa ghi chú"
        cancelText="Giữ lại"
        variant="danger"
        iconType="delete"
      />
    </div>
  );
};
