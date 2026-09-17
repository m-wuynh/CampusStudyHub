import React, { useState } from 'react';
import { 
  Layers, 
  Plus, 
  Search, 
  RotateCw, 
  ChevronLeft, 
  ChevronRight, 
  Share2, 
  Copy, 
  Check, 
  Lock, 
  Globe, 
  Sparkles, 
  Trash2, 
  Edit3, 
  ArrowLeft, 
  CheckCircle2, 
  XCircle, 
  Shuffle, 
  BookOpen,
  Download,
  Users
} from 'lucide-react';
import { FlashcardSet, FlashcardItem, Subject, UserProfile } from '../types';
import { subjectColorMap } from '../mockData';
import { Modal } from '../components/Modal';
import { ConfirmDialog } from '../components/ConfirmDialog';

interface FlashcardsViewProps {
  user: UserProfile;
  flashcardSets: FlashcardSet[];
  onSaveSet: (set: FlashcardSet) => void;
  onDeleteSet: (setId: string) => void;
  showToast: (msg: string, type?: 'success' | 'error' | 'info') => void;
  initialStudyingSetId?: string | null;
  onClearStudyingSetId?: () => void;
}

const subjectsList: Subject[] = ['Toán', 'Vật lý', 'Hóa học', 'Ngữ văn', 'Tiếng Anh', 'Sinh học'];

export const FlashcardsView: React.FC<FlashcardsViewProps> = ({
  user,
  flashcardSets,
  onSaveSet,
  onDeleteSet,
  showToast,
  initialStudyingSetId,
  onClearStudyingSetId,
}) => {
  // Navigation tabs: 'my-sets' vs 'shared-community'
  const [activeTab, setActiveTab] = useState<'my-sets' | 'shared-community'>('my-sets');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedSubject, setSelectedSubject] = useState<string>('all');

  // Currently studying set (if null, show sets list)
  const [studyingSet, setStudyingSet] = useState<FlashcardSet | null>(() => {
    if (initialStudyingSetId) {
      return flashcardSets.find(s => s.id === initialStudyingSetId) || null;
    }
    return null;
  });

  // Study player state
  const [currentCardIndex, setCurrentCardIndex] = useState(0);
  const [isFlipped, setIsFlipped] = useState(false);

  // Modals state
  const [isCreateSetModalOpen, setIsCreateSetModalOpen] = useState(false);
  const [newSetName, setNewSetName] = useState('');
  const [newSetSubject, setNewSetSubject] = useState<Subject>('Toán');
  const [newSetDesc, setNewSetDesc] = useState('');
  const [newSetPublic, setNewSetPublic] = useState(true);

  // Sharing modal
  const [sharingSet, setSharingSet] = useState<FlashcardSet | null>(null);
  const [hasCopiedLink, setHasCopiedLink] = useState(false);

  // Card editor modal (inside study mode)
  const [cardModalMode, setCardModalMode] = useState<'create' | 'edit' | null>(null);
  const [editingCard, setEditingCard] = useState<FlashcardItem | null>(null);
  const [cardQuestion, setCardQuestion] = useState('');
  const [cardAnswer, setCardAnswer] = useState('');

  // Delete set confirmation
  const [deleteSetConfirmId, setDeleteSetConfirmId] = useState<string | null>(null);

  // Filter sets
  const filteredSets = flashcardSets.filter((s) => {
    const isOwner = s.authorName === user.name || s.authorName === 'Nguyễn Minh Anh';
    const matchesTab = activeTab === 'my-sets' ? isOwner : s.isPublic;
    const matchesSubject = selectedSubject === 'all' || s.subject === selectedSubject;
    const matchesSearch = s.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      s.description.toLowerCase().includes(searchQuery.toLowerCase());
    return matchesTab && matchesSubject && matchesSearch;
  });

  // --- Handlers ---
  const handleOpenStudy = (set: FlashcardSet) => {
    setStudyingSet(set);
    setCurrentCardIndex(0);
    setIsFlipped(false);
  };

  const handleExitStudy = () => {
    setStudyingSet(null);
    if (onClearStudyingSetId) onClearStudyingSetId();
  };

  const handleNextCard = () => {
    if (!studyingSet) return;
    setIsFlipped(false);
    setCurrentCardIndex((prev) => (prev + 1) % studyingSet.cards.length);
  };

  const handlePrevCard = () => {
    if (!studyingSet) return;
    setIsFlipped(false);
    setCurrentCardIndex((prev) => (prev - 1 + studyingSet.cards.length) % studyingSet.cards.length);
  };

  const handleToggleCardLearned = (cardId: string, learnedStatus: boolean) => {
    if (!studyingSet) return;
    const updatedCards = studyingSet.cards.map(c => 
      c.id === cardId ? { ...c, isLearned: learnedStatus } : c
    );
    const updatedSet = { ...studyingSet, cards: updatedCards };
    setStudyingSet(updatedSet);
    onSaveSet(updatedSet);
    showToast(learnedStatus ? 'Đã ghi nhớ thẻ này!' : 'Đã đưa thẻ vào mục cần ôn lại.', 'info');
  };

  const handleShuffleCards = () => {
    if (!studyingSet) return;
    const shuffled = [...studyingSet.cards].sort(() => Math.random() - 0.5);
    const updated = { ...studyingSet, cards: shuffled };
    setStudyingSet(updated);
    setCurrentCardIndex(0);
    setIsFlipped(false);
    showToast('Đã trộn ngẫu nhiên thứ tự thẻ.', 'info');
  };

  const handleCreateSetSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newSetName.trim()) {
      showToast('Vui lòng nhập tên bộ thẻ.', 'error');
      return;
    }

    const newSet: FlashcardSet = {
      id: `set-${Date.now()}`,
      name: newSetName.trim(),
      subject: newSetSubject,
      description: newSetDesc.trim() || 'Bộ thẻ ghi nhớ do học sinh tự biên soạn.',
      cards: [
        {
          id: `c-${Date.now()}-1`,
          question: 'Câu hỏi mẫu: Hãy điền kiến thức cần nhớ vào đây',
          answer: 'Đáp án mẫu: Câu trả lời ngắn gọn, chuẩn xác',
          isLearned: false
        }
      ],
      isPublic: newSetPublic,
      authorName: user.name,
      authorAvatar: user.avatarUrl,
      createdAt: new Date().toISOString().slice(0, 10),
      downloadsCount: 0
    };

    onSaveSet(newSet);
    setIsCreateSetModalOpen(false);
    setNewSetName('');
    setNewSetDesc('');
    showToast('Tạo bộ flashcard mới thành công!', 'success');
    handleOpenStudy(newSet);
  };

  const handleCardSaveSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!studyingSet || !cardQuestion.trim() || !cardAnswer.trim()) {
      showToast('Vui lòng nhập đủ câu hỏi và câu trả lời.', 'error');
      return;
    }

    let updatedCards: FlashcardItem[] = [];
    if (cardModalMode === 'create') {
      const newCard: FlashcardItem = {
        id: `c-${Date.now()}`,
        question: cardQuestion.trim(),
        answer: cardAnswer.trim(),
        isLearned: false
      };
      updatedCards = [...studyingSet.cards, newCard];
      showToast('Đã thêm thẻ mới vào bộ.', 'success');
    } else if (cardModalMode === 'edit' && editingCard) {
      updatedCards = studyingSet.cards.map(c => 
        c.id === editingCard.id ? { ...c, question: cardQuestion.trim(), answer: cardAnswer.trim() } : c
      );
      showToast('Đã cập nhật thẻ.', 'success');
    }

    const updatedSet = { ...studyingSet, cards: updatedCards };
    setStudyingSet(updatedSet);
    onSaveSet(updatedSet);
    setCardModalMode(null);
    setEditingCard(null);
  };

  const handleDeleteCard = (cardId: string) => {
    if (!studyingSet) return;
    if (studyingSet.cards.length <= 1) {
      showToast('Bộ thẻ phải có ít nhất 1 thẻ ghi nhớ.', 'error');
      return;
    }
    const updatedCards = studyingSet.cards.filter(c => c.id !== cardId);
    const updatedSet = { ...studyingSet, cards: updatedCards };
    setStudyingSet(updatedSet);
    onSaveSet(updatedSet);
    if (currentCardIndex >= updatedCards.length) {
      setCurrentCardIndex(Math.max(0, updatedCards.length - 1));
    }
    showToast('Đã xóa thẻ.', 'info');
  };

  const handleOpenShare = (set: FlashcardSet, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    setSharingSet(set);
    setHasCopiedLink(false);
  };

  const handleCopyShareLink = () => {
    navigator.clipboard?.writeText?.(window.location.origin + `/share/flashcards/${sharingSet?.id}`);
    setHasCopiedLink(true);
    showToast('Đã sao chép liên kết chia sẻ vào clipboard!', 'success');
    setTimeout(() => setHasCopiedLink(false), 3000);
  };

  const handleForkSharedSet = (set: FlashcardSet, e: React.MouseEvent) => {
    e.stopPropagation();
    const clonedSet: FlashcardSet = {
      ...set,
      id: `set-fork-${Date.now()}`,
      name: `${set.name} (Bản sao)`,
      authorName: user.name,
      authorAvatar: user.avatarUrl,
      createdAt: new Date().toISOString().slice(0, 10),
      isPublic: false,
    };
    onSaveSet(clonedSet);
    showToast(`Đã lưu bộ thẻ "${set.name}" vào kho thẻ của bạn!`, 'success');
  };

  // --- Render Mode: Studying interactive player ---
  if (studyingSet) {
    const currentCard = studyingSet.cards[currentCardIndex] || studyingSet.cards[0];
    const totalCards = studyingSet.cards.length;
    const learnedCount = studyingSet.cards.filter(c => c.isLearned).length;
    const progressPercent = Math.round((learnedCount / totalCards) * 100);

    return (
      <div id="flashcard-study-screen" className="max-w-4xl mx-auto space-y-6 pb-12">
        {/* Navigation & Set Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 bg-white p-4 rounded-2xl border border-slate-200 shadow-2xs">
          <div className="flex items-center gap-3">
            <button
              id="back-to-sets-btn"
              onClick={handleExitStudy}
              className="p-2 text-slate-500 hover:text-slate-900 hover:bg-slate-100 rounded-xl transition-colors"
              title="Quay lại danh sách bộ thẻ"
            >
              <ArrowLeft className="w-5 h-5" />
            </button>
            <div>
              <div className="flex items-center gap-2">
                <span className={`text-[10px] font-bold px-2 py-0.5 rounded-md ${
                  subjectColorMap[studyingSet.subject]?.bg || 'bg-slate-100'
                } ${subjectColorMap[studyingSet.subject]?.text || 'text-slate-800'} border ${
                  subjectColorMap[studyingSet.subject]?.border || 'border-slate-200'
                }`}>
                  {studyingSet.subject}
                </span>
                <span className="text-xs text-slate-400">• Tác giả: {studyingSet.authorName}</span>
              </div>
              <h2 className="text-base sm:text-lg font-bold text-slate-900 leading-tight mt-0.5">
                {studyingSet.name}
              </h2>
            </div>
          </div>

          <div className="flex items-center gap-2 self-end sm:self-auto">
            <button
              id="study-share-btn"
              onClick={() => handleOpenShare(studyingSet)}
              className="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold rounded-xl transition-colors flex items-center gap-1.5"
            >
              <Share2 className="w-3.5 h-3.5" />
              <span>Chia sẻ</span>
            </button>
            <button
              id="study-add-card-btn"
              onClick={() => {
                setCardModalMode('create');
                setCardQuestion('');
                setCardAnswer('');
              }}
              className="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs transition-colors flex items-center gap-1.5"
            >
              <Plus className="w-4 h-4" />
              <span>Thêm thẻ mới</span>
            </button>
          </div>
        </div>

        {/* Study Progress Bar */}
        <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-2xs">
          <div className="flex items-center justify-between text-xs font-semibold text-slate-600 mb-2">
            <span>Tiến độ ghi nhớ bộ thẻ</span>
            <span className="text-emerald-600 font-bold">
              Đã nhớ {learnedCount} / {totalCards} ({progressPercent}%)
            </span>
          </div>
          <div className="w-full bg-slate-100 h-2 rounded-full overflow-hidden">
            <div
              className="bg-emerald-500 h-full rounded-full transition-all duration-300"
              style={{ width: `${progressPercent}%` }}
            />
          </div>
        </div>

        {/* Large Centered Flashcard with 3D Flip */}
        <div className="flex flex-col items-center">
          <div className="w-full text-center text-xs font-bold text-slate-400 uppercase tracking-wider mb-2">
            Thẻ {currentCardIndex + 1} / {totalCards} • Nhấn vào thẻ để lật
          </div>

          <div
            id="interactive-flashcard"
            onClick={() => setIsFlipped(!isFlipped)}
            className="w-full max-w-2xl min-h-[300px] sm:min-h-[360px] bg-white rounded-3xl border-2 border-slate-200 shadow-lg cursor-pointer p-8 sm:p-12 flex flex-col justify-between transition-all duration-300 hover:border-indigo-400 hover:shadow-xl relative select-none"
            style={{ perspective: '1000px' }}
          >
            {/* Top card pill: Question vs Answer */}
            <div className="flex items-center justify-between">
              <span className={`px-3 py-1 text-xs font-bold rounded-full ${
                isFlipped
                  ? 'bg-emerald-100 text-emerald-800 border border-emerald-200'
                  : 'bg-indigo-100 text-indigo-800 border border-indigo-200'
              }`}>
                {isFlipped ? '💡 ĐÁP ÁN (MẶT SAU)' : '❓ CÂU HỎI (MẶT TRƯỚC)'}
              </span>

              <div className="flex items-center gap-1.5 text-slate-400 hover:text-slate-600 text-xs">
                <RotateCw className="w-4 h-4 animate-spin-reverse" />
                <span className="hidden sm:inline">Nhấn để lật</span>
              </div>
            </div>

            {/* Main Centered Content */}
            <div className="my-auto py-6 text-center">
              <p className={`font-sans leading-relaxed transition-all duration-200 ${
                isFlipped
                  ? 'text-lg sm:text-2xl font-bold text-emerald-900'
                  : 'text-lg sm:text-2xl font-bold text-slate-900'
              }`}>
                {isFlipped ? currentCard?.answer : currentCard?.question}
              </p>
            </div>

            {/* Bottom Card Footer with status */}
            <div className="flex items-center justify-between pt-4 border-t border-slate-100 text-xs">
              <span className="text-slate-400 font-medium">
                {currentCard?.isLearned ? '✅ Đã đánh dấu nhớ' : '⏳ Cần rèn luyện thêm'}
              </span>

              <div className="flex items-center gap-2" onClick={(e) => e.stopPropagation()}>
                <button
                  onClick={() => {
                    setCardModalMode('edit');
                    setEditingCard(currentCard);
                    setCardQuestion(currentCard.question);
                    setCardAnswer(currentCard.answer);
                  }}
                  className="p-1.5 text-slate-400 hover:text-indigo-600 hover:bg-indigo-50 rounded-lg transition-colors"
                  title="Sửa thẻ này"
                >
                  <Edit3 className="w-4 h-4" />
                </button>
                <button
                  onClick={() => handleDeleteCard(currentCard.id)}
                  className="p-1.5 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors"
                  title="Xóa thẻ này"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            </div>
          </div>

          {/* Card Control Buttons: Previous, Flip, Next */}
          <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
            <button
              id="prev-card-btn"
              onClick={handlePrevCard}
              className="px-5 py-2.5 bg-white hover:bg-slate-100 text-slate-700 text-xs font-bold rounded-xl border border-slate-300 shadow-2xs transition-colors flex items-center gap-2"
            >
              <ChevronLeft className="w-4 h-4" />
              <span>Thẻ trước</span>
            </button>

            <button
              id="flip-card-btn"
              onClick={() => setIsFlipped(!isFlipped)}
              className="px-6 py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold rounded-xl shadow-xs transition-colors flex items-center gap-2"
            >
              <RotateCw className="w-4 h-4" />
              <span>{isFlipped ? 'Xem câu hỏi' : 'Lật xem đáp án'}</span>
            </button>

            <button
              id="next-card-btn"
              onClick={handleNextCard}
              className="px-5 py-2.5 bg-white hover:bg-slate-100 text-slate-700 text-xs font-bold rounded-xl border border-slate-300 shadow-2xs transition-colors flex items-center gap-2"
            >
              <span>Thẻ tiếp</span>
              <ChevronRight className="w-4 h-4" />
            </button>

            <button
              id="shuffle-cards-btn"
              onClick={handleShuffleCards}
              className="p-2.5 bg-white hover:bg-slate-100 text-slate-500 rounded-xl border border-slate-200 transition-colors"
              title="Xáo trộn ngẫu nhiên thứ tự thẻ"
            >
              <Shuffle className="w-4 h-4" />
            </button>
          </div>

          {/* Quick Mastery Evaluation Buttons */}
          <div className="mt-4 flex items-center gap-3">
            <button
              id="mark-unlearned-btn"
              onClick={() => handleToggleCardLearned(currentCard.id, false)}
              className="px-4 py-2 bg-rose-50 hover:bg-rose-100 text-rose-700 text-xs font-semibold rounded-xl border border-rose-200 flex items-center gap-1.5 transition-colors"
            >
              <XCircle className="w-4 h-4 text-rose-500" />
              <span>Chưa thuộc (Học lại)</span>
            </button>
            <button
              id="mark-learned-btn"
              onClick={() => handleToggleCardLearned(currentCard.id, true)}
              className="px-4 py-2 bg-emerald-50 hover:bg-emerald-100 text-emerald-700 text-xs font-semibold rounded-xl border border-emerald-200 flex items-center gap-1.5 transition-colors"
            >
              <CheckCircle2 className="w-4 h-4 text-emerald-600" />
              <span>Đã nhớ thẻ này</span>
            </button>
          </div>
        </div>

        {/* Detailed Cards List in this set */}
        <div className="bg-white rounded-2xl border border-slate-200 p-6 shadow-2xs">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100">
            <h3 className="text-sm font-bold text-slate-900">
              Tất cả các thẻ trong bộ ({studyingSet.cards.length})
            </h3>
            <span className="text-xs text-slate-500">Bấm vào thẻ bất kỳ để nhảy đến</span>
          </div>

          <div className="mt-4 divide-y divide-slate-100 max-h-80 overflow-y-auto">
            {studyingSet.cards.map((card, idx) => (
              <div
                key={card.id}
                onClick={() => {
                  setCurrentCardIndex(idx);
                  setIsFlipped(false);
                }}
                className={`py-3 px-3 rounded-xl flex items-center justify-between gap-4 cursor-pointer transition-colors ${
                  idx === currentCardIndex ? 'bg-indigo-50/80 border border-indigo-200' : 'hover:bg-slate-50'
                }`}
              >
                <div className="flex items-center gap-3 min-w-0">
                  <span className="text-xs font-mono font-bold text-slate-400 w-6">#{idx + 1}</span>
                  <div className="min-w-0">
                    <p className="text-xs font-bold text-slate-900 truncate">{card.question}</p>
                    <p className="text-[11px] text-slate-500 truncate mt-0.5">{card.answer}</p>
                  </div>
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  {card.isLearned ? (
                    <span className="text-[10px] font-bold px-2 py-0.5 bg-emerald-100 text-emerald-700 rounded-md">Đã nhớ</span>
                  ) : (
                    <span className="text-[10px] font-bold px-2 py-0.5 bg-slate-100 text-slate-500 rounded-md">Chưa nhớ</span>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Add/Edit Card Modal inside Study View */}
        <Modal
          isOpen={cardModalMode !== null}
          onClose={() => setCardModalMode(null)}
          title={cardModalMode === 'create' ? 'Thêm thẻ ghi nhớ mới' : 'Chỉnh sửa thẻ ghi nhớ'}
          subtitle={`Bộ thẻ: ${studyingSet.name}`}
        >
          <form onSubmit={handleCardSaveSubmit} className="space-y-4">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Mặt trước: Câu hỏi / Thuật ngữ / Khái niệm
              </label>
              <textarea
                required
                rows={3}
                placeholder="Ví dụ: Định luật bảo toàn cơ năng trong trọng trường?"
                value={cardQuestion}
                onChange={(e) => setCardQuestion(e.target.value)}
                className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:border-indigo-500 outline-none"
              />
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Mặt sau: Đáp án / Lời giải thích / Công thức
              </label>
              <textarea
                required
                rows={3}
                placeholder="Ví dụ: W = W_đ + W_t = const ⟺ (1/2)mv² + mgh = hằng số"
                value={cardAnswer}
                onChange={(e) => setCardAnswer(e.target.value)}
                className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:border-indigo-500 outline-none font-mono"
              />
            </div>

            <div className="flex items-center justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setCardModalMode(null)}
                className="px-4 py-2 text-xs font-semibold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl"
              >
                Hủy
              </button>
              <button
                type="submit"
                className="px-4 py-2 text-xs font-semibold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl shadow-2xs"
              >
                {cardModalMode === 'create' ? 'Thêm thẻ' : 'Lưu thay đổi'}
              </button>
            </div>
          </form>
        </Modal>

        {/* Share Dialog */}
        {sharingSet && (
          <Modal
            isOpen={true}
            onClose={() => setSharingSet(null)}
            title="Chia sẻ bộ thẻ Flashcard"
            subtitle={sharingSet.name}
            maxWidth="md"
          >
            <div className="space-y-4">
              <p className="text-xs text-slate-600 leading-relaxed">
                Chia sẻ bộ thẻ này với bạn bè cùng lớp hoặc thành viên nhóm học tập để cùng ôn luyện.
              </p>

              <div className="p-3 bg-slate-50 border border-slate-200 rounded-xl flex items-center justify-between">
                <div className="flex items-center gap-2">
                  {sharingSet.isPublic ? (
                    <Globe className="w-5 h-5 text-emerald-600 shrink-0" />
                  ) : (
                    <Lock className="w-5 h-5 text-amber-600 shrink-0" />
                  )}
                  <div>
                    <p className="text-xs font-bold text-slate-900">
                      {sharingSet.isPublic ? 'Công khai (Public)' : 'Riêng tư (Private)'}
                    </p>
                    <p className="text-[11px] text-slate-500">
                      {sharingSet.isPublic ? 'Bất kỳ ai có liên kết đều có thể học bộ thẻ này' : 'Chỉ mình bạn có thể truy cập'}
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => {
                    const updated = { ...sharingSet, isPublic: !sharingSet.isPublic };
                    setSharingSet(updated);
                    onSaveSet(updated);
                    showToast(`Đã chuyển sang chế độ ${updated.isPublic ? 'Công khai' : 'Riêng tư'}.`, 'info');
                  }}
                  className="text-xs text-indigo-600 hover:text-indigo-800 font-semibold px-2 py-1 bg-white border border-slate-200 rounded-lg"
                >
                  Thay đổi
                </button>
              </div>

              <div>
                <label className="block text-[11px] font-bold text-slate-500 uppercase tracking-wider mb-1">
                  Đường dẫn liên kết học tập
                </label>
                <div className="flex items-center gap-2">
                  <input
                    type="text"
                    readOnly
                    value={`https://studyhub.edu.vn/flashcards/${sharingSet.id}`}
                    className="flex-1 px-3 py-2 text-xs font-mono text-slate-700 bg-slate-100 border border-slate-200 rounded-xl outline-none"
                  />
                  <button
                    id="copy-share-link-btn"
                    type="button"
                    onClick={handleCopyShareLink}
                    className="px-3.5 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl flex items-center gap-1.5 shrink-0 transition-colors"
                  >
                    {hasCopiedLink ? <Check className="w-3.5 h-3.5" /> : <Copy className="w-3.5 h-3.5" />}
                    <span>{hasCopiedLink ? 'Đã chép' : 'Sao chép'}</span>
                  </button>
                </div>
              </div>

              <div className="pt-2 flex justify-end">
                <button
                  onClick={() => setSharingSet(null)}
                  className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold rounded-xl"
                >
                  Đóng
                </button>
              </div>
            </div>
          </Modal>
        )}
      </div>
    );
  }

  // --- Render Mode: Flashcard Sets List ---
  return (
    <div id="flashcards-view" className="space-y-6 max-w-7xl mx-auto pb-10">
      {/* Top Header & Tabs Bar */}
      <div className="bg-white p-4 sm:p-5 rounded-2xl border border-slate-200 shadow-2xs flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-4">
        {/* Navigation Tabs */}
        <div className="flex items-center gap-2 bg-slate-100 p-1 rounded-xl">
          <button
            id="tab-my-flashcards"
            onClick={() => setActiveTab('my-sets')}
            className={`px-4 py-2 text-xs font-bold rounded-lg transition-all ${
              activeTab === 'my-sets'
                ? 'bg-white text-indigo-600 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Bộ thẻ của tôi
          </button>
          <button
            id="tab-shared-flashcards"
            onClick={() => setActiveTab('shared-community')}
            className={`px-4 py-2 text-xs font-bold rounded-lg transition-all flex items-center gap-1.5 ${
              activeTab === 'shared-community'
                ? 'bg-white text-indigo-600 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <Globe className="w-3.5 h-3.5 text-emerald-600" />
            <span>Thư viện chia sẻ (Cộng đồng)</span>
          </button>
        </div>

        {/* Action Button: Create Set */}
        <button
          id="create-flashcard-set-btn"
          onClick={() => {
            setIsCreateSetModalOpen(true);
            setNewSetName('');
            setNewSetDesc('');
          }}
          className="flex items-center justify-center gap-1.5 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs transition-colors shrink-0"
        >
          <Plus className="w-4 h-4" />
          <span>Tạo bộ Flashcard mới</span>
        </button>
      </div>

      {/* Subject Filter & Search */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3 bg-white p-3.5 rounded-2xl border border-slate-200 shadow-2xs">
        <div className="flex items-center gap-1.5 overflow-x-auto pb-1 sm:pb-0">
          <button
            onClick={() => setSelectedSubject('all')}
            className={`px-3 py-1.5 rounded-xl text-xs font-semibold whitespace-nowrap transition-colors ${
              selectedSubject === 'all'
                ? 'bg-indigo-600 text-white shadow-2xs'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            Tất cả ({flashcardSets.length})
          </button>
          {subjectsList.map((sub) => {
            const count = flashcardSets.filter(s => s.subject === sub).length;
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

        <div className="relative sm:w-64">
          <Search className="w-3.5 h-3.5 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none" />
          <input
            type="text"
            placeholder="Tìm theo tên bộ thẻ, môn học..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full pl-8 pr-3 py-1.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-800 focus:bg-white focus:border-indigo-400 outline-none"
          />
        </div>
      </div>

      {/* Flashcard Sets Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
        {filteredSets.map((set) => {
          const learnedCount = set.cards.filter(c => c.isLearned).length;
          const percent = Math.round((learnedCount / set.cards.length) * 100);
          const color = subjectColorMap[set.subject] || subjectColorMap['Toán'];
          const isMySet = set.authorName === user.name || set.authorName === 'Nguyễn Minh Anh';

          return (
            <div
              key={set.id}
              id={`flashcard-set-card-${set.id}`}
              className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs hover:shadow-md hover:border-indigo-300 transition-all flex flex-col justify-between"
            >
              <div>
                {/* Subject badge, Visibility & Options */}
                <div className="flex items-center justify-between gap-2 mb-3">
                  <span className={`text-[10px] font-bold px-2 py-0.5 rounded-md ${color.bg} ${color.text} border ${color.border}`}>
                    {set.subject}
                  </span>

                  <div className="flex items-center gap-1.5">
                    {set.isPublic ? (
                      <span className="px-2 py-0.5 text-[10px] font-semibold bg-emerald-50 text-emerald-700 rounded-full border border-emerald-200 flex items-center gap-1">
                        <Globe className="w-3 h-3" /> Công khai
                      </span>
                    ) : (
                      <span className="px-2 py-0.5 text-[10px] font-semibold bg-slate-100 text-slate-600 rounded-full border border-slate-200 flex items-center gap-1">
                        <Lock className="w-3 h-3" /> Riêng tư
                      </span>
                    )}

                    <button
                      onClick={(e) => handleOpenShare(set, e)}
                      className="p-1.5 text-slate-400 hover:text-indigo-600 hover:bg-slate-100 rounded-lg transition-colors"
                      title="Chia sẻ bộ thẻ"
                    >
                      <Share2 className="w-3.5 h-3.5" />
                    </button>

                    {isMySet && (
                      <button
                        onClick={() => setDeleteSetConfirmId(set.id)}
                        className="p-1.5 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors"
                        title="Xóa bộ thẻ"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </div>
                </div>

                <h3 className="text-sm font-bold text-slate-900 leading-snug">
                  {set.name}
                </h3>
                <p className="text-xs text-slate-500 mt-1.5 line-clamp-2 leading-relaxed">
                  {set.description}
                </p>

                {/* Progress bar */}
                <div className="mt-4 pt-3 border-t border-slate-100">
                  <div className="flex items-center justify-between text-[11px] font-semibold text-slate-600 mb-1">
                    <span>{set.cards.length} thẻ ghi nhớ</span>
                    <span className="text-emerald-600">{learnedCount}/{set.cards.length} đã thuộc</span>
                  </div>
                  <div className="w-full bg-slate-100 h-1.5 rounded-full overflow-hidden">
                    <div className="bg-emerald-500 h-full rounded-full" style={{ width: `${percent}%` }} />
                  </div>
                </div>
              </div>

              {/* Author & Action buttons */}
              <div className="mt-5 pt-3 border-t border-slate-100 flex items-center justify-between gap-2">
                <div className="flex items-center gap-2 min-w-0">
                  {set.authorAvatar ? (
                    <img src={set.authorAvatar} alt={set.authorName} className="w-6 h-6 rounded-full object-cover shrink-0" />
                  ) : (
                    <div className="w-6 h-6 rounded-full bg-slate-200 text-slate-600 text-[10px] font-bold flex items-center justify-center">
                      {set.authorName.charAt(0)}
                    </div>
                  )}
                  <span className="text-[11px] text-slate-500 truncate font-medium">{set.authorName}</span>
                </div>

                <div className="flex items-center gap-1.5">
                  {!isMySet && (
                    <button
                      onClick={(e) => handleForkSharedSet(set, e)}
                      className="px-2.5 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold rounded-xl flex items-center gap-1"
                      title="Lưu bản sao về kho của tôi"
                    >
                      <Download className="w-3.5 h-3.5" />
                    </button>
                  )}
                  <button
                    onClick={() => handleOpenStudy(set)}
                    className="px-4 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs transition-colors"
                  >
                    Bắt đầu học
                  </button>
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {filteredSets.length === 0 && (
        <div className="bg-white rounded-2xl border border-slate-200 p-12 text-center">
          <Layers className="w-12 h-12 text-slate-300 mx-auto mb-3" />
          <h3 className="text-sm font-bold text-slate-800">Không tìm thấy bộ flashcard nào</h3>
          <p className="text-xs text-slate-500 mt-1 max-w-sm mx-auto">
            {activeTab === 'my-sets'
              ? 'Bạn chưa tạo bộ thẻ nào cho môn này. Hãy tạo ngay bộ thẻ đầu tiên để ôn tập hiệu quả.'
              : 'Chưa có bộ thẻ công khai nào phù hợp với bộ lọc.'}
          </p>
          <button
            onClick={() => setIsCreateSetModalOpen(true)}
            className="mt-4 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs"
          >
            + Tạo bộ thẻ mới
          </button>
        </div>
      )}

      {/* Create Set Modal */}
      <Modal
        isOpen={isCreateSetModalOpen}
        onClose={() => setIsCreateSetModalOpen(false)}
        title="Tạo bộ thẻ Flashcard mới"
        subtitle="Hệ thống thẻ ôn tập nhanh câu hỏi và câu trả lời"
      >
        <form onSubmit={handleCreateSetSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">
              Tên bộ thẻ (Flashcard Set Name)
            </label>
            <input
              type="text"
              required
              placeholder="Ví dụ: Công thức Vật lý 11 - Cơ năng, Từ vựng Unit 2..."
              value={newSetName}
              onChange={(e) => setNewSetName(e.target.value)}
              className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:border-indigo-500 outline-none"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Môn học</label>
              <select
                value={newSetSubject}
                onChange={(e) => setNewSetSubject(e.target.value as Subject)}
                className="w-full px-3 py-2 text-xs text-slate-800 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:border-indigo-500 outline-none"
              >
                {subjectsList.map((sub) => (
                  <option key={sub} value={sub}>{sub}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Quyền truy cập</label>
              <select
                value={newSetPublic ? 'public' : 'private'}
                onChange={(e) => setNewSetPublic(e.target.value === 'public')}
                className="w-full px-3 py-2 text-xs text-slate-800 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:border-indigo-500 outline-none"
              >
                <option value="public">Công khai (Mọi người cùng xem)</option>
                <option value="private">Riêng tư (Chỉ mình tôi)</option>
              </select>
            </div>
          </div>

          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">Mô tả ngắn</label>
            <textarea
              rows={2}
              placeholder="Ghi chú mục tiêu ôn tập của bộ thẻ này..."
              value={newSetDesc}
              onChange={(e) => setNewSetDesc(e.target.value)}
              className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:border-indigo-500 outline-none"
            />
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
            <button
              type="button"
              onClick={() => setIsCreateSetModalOpen(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl"
            >
              Hủy
            </button>
            <button
              type="submit"
              className="px-4 py-2 text-xs font-semibold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl shadow-2xs"
            >
              Tạo bộ thẻ và soạn nội dung
            </button>
          </div>
        </form>
      </Modal>

      {/* Share Dialog */}
      {sharingSet && (
        <Modal
          isOpen={true}
          onClose={() => setSharingSet(null)}
          title="Chia sẻ bộ thẻ Flashcard"
          subtitle={sharingSet.name}
          maxWidth="md"
        >
          <div className="space-y-4">
            <p className="text-xs text-slate-600 leading-relaxed">
              Chia sẻ bộ thẻ này với bạn bè cùng lớp hoặc thành viên nhóm học tập để cùng ôn luyện.
            </p>

            <div className="p-3 bg-slate-50 border border-slate-200 rounded-xl flex items-center justify-between">
              <div className="flex items-center gap-2">
                {sharingSet.isPublic ? (
                  <Globe className="w-5 h-5 text-emerald-600 shrink-0" />
                ) : (
                  <Lock className="w-5 h-5 text-amber-600 shrink-0" />
                )}
                <div>
                  <p className="text-xs font-bold text-slate-900">
                    {sharingSet.isPublic ? 'Công khai (Public)' : 'Riêng tư (Private)'}
                  </p>
                  <p className="text-[11px] text-slate-500">
                    {sharingSet.isPublic ? 'Bất kỳ ai có liên kết đều có thể học bộ thẻ này' : 'Chỉ mình bạn có thể truy cập'}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => {
                  const updated = { ...sharingSet, isPublic: !sharingSet.isPublic };
                  setSharingSet(updated);
                  onSaveSet(updated);
                  showToast(`Đã chuyển sang chế độ ${updated.isPublic ? 'Công khai' : 'Riêng tư'}.`, 'info');
                }}
                className="text-xs text-indigo-600 hover:text-indigo-800 font-semibold px-2 py-1 bg-white border border-slate-200 rounded-lg"
              >
                Thay đổi
              </button>
            </div>

            <div>
              <label className="block text-[11px] font-bold text-slate-500 uppercase tracking-wider mb-1">
                Đường dẫn liên kết học tập
              </label>
              <div className="flex items-center gap-2">
                <input
                  type="text"
                  readOnly
                  value={`https://studyhub.edu.vn/flashcards/${sharingSet.id}`}
                  className="flex-1 px-3 py-2 text-xs font-mono text-slate-700 bg-slate-100 border border-slate-200 rounded-xl outline-none"
                />
                <button
                  type="button"
                  onClick={handleCopyShareLink}
                  className="px-3.5 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl flex items-center gap-1.5 shrink-0 transition-colors"
                >
                  {hasCopiedLink ? <Check className="w-3.5 h-3.5" /> : <Copy className="w-3.5 h-3.5" />}
                  <span>{hasCopiedLink ? 'Đã chép' : 'Sao chép'}</span>
                </button>
              </div>
            </div>

            <div className="pt-2 flex justify-end">
              <button
                onClick={() => setSharingSet(null)}
                className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold rounded-xl"
              >
                Đóng
              </button>
            </div>
          </div>
        </Modal>
      )}

      {/* Delete Confirmation */}
      <ConfirmDialog
        isOpen={!!deleteSetConfirmId}
        onClose={() => setDeleteSetConfirmId(null)}
        onConfirm={() => {
          if (deleteSetConfirmId) {
            onDeleteSet(deleteSetConfirmId);
            showToast('Đã xóa bộ thẻ flashcard.', 'info');
            setDeleteSetConfirmId(null);
          }
        }}
        title="Xóa bộ thẻ này?"
        message="Tất cả các thẻ bên trong bộ thẻ này sẽ bị xóa khỏi tài khoản của bạn. Bạn có chắc chắn không?"
        confirmText="Xóa bộ thẻ"
        cancelText="Hủy"
        variant="danger"
        iconType="delete"
      />
    </div>
  );
};
