import React, { useState } from 'react';
import { 
  Users, 
  Plus, 
  Search, 
  ArrowLeft, 
  Megaphone, 
  FileText, 
  Layers, 
  UserPlus, 
  Check, 
  Calendar, 
  ShieldCheck, 
  Share2, 
  Copy,
  BookOpen
} from 'lucide-react';
import { StudyGroup, Subject, UserProfile, Note, FlashcardSet } from '../types';
import { subjectColorMap } from '../mockData';
import { Modal } from '../components/Modal';

interface StudyGroupsViewProps {
  user: UserProfile;
  groups: StudyGroup[];
  notes: Note[];
  flashcardSets: FlashcardSet[];
  onSaveGroup: (group: StudyGroup) => void;
  showToast: (msg: string, type?: 'success' | 'error' | 'info') => void;
  onOpenNote: (noteId: string) => void;
  onOpenStudySet: (setId: string) => void;
}

const subjectsList: Subject[] = ['Toán', 'Vật lý', 'Hóa học', 'Ngữ văn', 'Tiếng Anh', 'Sinh học'];

export const StudyGroupsView: React.FC<StudyGroupsViewProps> = ({
  user,
  groups,
  notes,
  flashcardSets,
  onSaveGroup,
  showToast,
  onOpenNote,
  onOpenStudySet,
}) => {
  // Navigation mode: list vs detail
  const [selectedGroup, setSelectedGroup] = useState<StudyGroup | null>(null);

  // Group detail active tab: 'announcements' | 'notes' | 'flashcards' | 'members'
  const [detailTab, setDetailTab] = useState<'announcements' | 'notes' | 'flashcards' | 'members'>('announcements');

  // Search & filter
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedSubject, setSelectedSubject] = useState<string>('all');
  const [groupViewFilter, setGroupViewFilter] = useState<'my' | 'all'>('my');

  // Create Group modal
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [newGroupName, setNewGroupName] = useState('');
  const [newGroupSubject, setNewGroupSubject] = useState<Subject>('Toán');
  const [newGroupDesc, setNewGroupDesc] = useState('');

  // New Announcement modal
  const [isNewAnnouncementModalOpen, setIsNewAnnouncementModalOpen] = useState(false);
  const [announcementTitle, setAnnouncementTitle] = useState('');
  const [announcementContent, setAnnouncementContent] = useState('');

  // Share note to group modal
  const [isShareNoteModalOpen, setIsShareNoteModalOpen] = useState(false);
  const [noteToShareId, setNoteToShareId] = useState(notes[0]?.id || '');

  // Filter groups
  const filteredGroups = groups.filter((g) => {
    const matchesTab = groupViewFilter === 'my' ? g.isMember : true;
    const matchesSubject = selectedSubject === 'all' || g.subject === selectedSubject;
    const matchesSearch = g.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      g.description.toLowerCase().includes(searchQuery.toLowerCase());
    return matchesTab && matchesSubject && matchesSearch;
  });

  const handleCreateGroupSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newGroupName.trim()) {
      showToast('Vui lòng nhập tên nhóm học tập.', 'error');
      return;
    }

    const newGroup: StudyGroup = {
      id: `grp-${Date.now()}`,
      name: newGroupName.trim(),
      subject: newGroupSubject,
      description: newGroupDesc.trim() || 'Nhóm học sinh trao đổi bài vở.',
      ownerName: user.name,
      memberCount: 1,
      isMember: true,
      sharedNoteIds: [],
      sharedSetIds: [],
      members: [
        {
          id: `m-${Date.now()}`,
          name: user.name,
          role: 'Trưởng nhóm',
          avatar: user.avatarUrl,
          grade: user.grade,
        }
      ],
      announcements: [
        {
          id: `anc-${Date.now()}`,
          author: user.name,
          authorAvatar: user.avatarUrl,
          date: new Date().toISOString().slice(0, 16).replace('T', ' '),
          title: `Chào mừng thành viên đến với nhóm ${newGroupName.trim()}`,
          content: 'Nhóm đã được khởi tạo. Hãy cùng chia sẻ tài liệu và hỗ trợ nhau học tập tốt nhé!'
        }
      ]
    };

    onSaveGroup(newGroup);
    setIsCreateModalOpen(false);
    setSelectedGroup(newGroup);
    showToast(`Đã tạo nhóm "${newGroupName.trim()}" thành công!`, 'success');
  };

  const handleToggleJoinGroup = (group: StudyGroup, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    const updatedMemberStatus = !group.isMember;
    const updatedMembers = updatedMemberStatus
      ? [...group.members, { id: `m-${Date.now()}`, name: user.name, role: 'Thành viên' as const, avatar: user.avatarUrl, grade: user.grade }]
      : group.members.filter(m => m.name !== user.name);

    const updatedGroup: StudyGroup = {
      ...group,
      isMember: updatedMemberStatus,
      memberCount: updatedMemberStatus ? group.memberCount + 1 : Math.max(1, group.memberCount - 1),
      members: updatedMembers,
    };

    onSaveGroup(updatedGroup);
    if (selectedGroup?.id === group.id) {
      setSelectedGroup(updatedGroup);
    }
    showToast(updatedMemberStatus ? `Đã tham gia nhóm ${group.name}!` : `Đã rời nhóm ${group.name}.`, 'info');
  };

  const handlePostAnnouncement = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedGroup || !announcementTitle.trim() || !announcementContent.trim()) {
      showToast('Vui lòng điền đủ tiêu đề và nội dung thông báo.', 'error');
      return;
    }

    const newAnc = {
      id: `anc-${Date.now()}`,
      author: user.name,
      authorAvatar: user.avatarUrl,
      date: new Date().toISOString().slice(0, 16).replace('T', ' '),
      title: announcementTitle.trim(),
      content: announcementContent.trim(),
    };

    const updated = {
      ...selectedGroup,
      announcements: [newAnc, ...selectedGroup.announcements],
    };

    setSelectedGroup(updated);
    onSaveGroup(updated);
    setIsNewAnnouncementModalOpen(false);
    setAnnouncementTitle('');
    setAnnouncementContent('');
    showToast('Đã đăng thông báo cho nhóm!', 'success');
  };

  const handleShareNoteToGroup = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedGroup || !noteToShareId) return;

    if (selectedGroup.sharedNoteIds.includes(noteToShareId)) {
      showToast('Ghi chú này đã được chia sẻ trong nhóm rồi.', 'info');
      setIsShareNoteModalOpen(false);
      return;
    }

    const updated = {
      ...selectedGroup,
      sharedNoteIds: [...selectedGroup.sharedNoteIds, noteToShareId],
    };

    setSelectedGroup(updated);
    onSaveGroup(updated);
    setIsShareNoteModalOpen(false);
    showToast('Đã chia sẻ tài liệu ghi chú vào nhóm!', 'success');
  };

  // --- Render Mode: GROUP DETAIL VIEW ---
  if (selectedGroup) {
    const color = subjectColorMap[selectedGroup.subject] || subjectColorMap['Toán'];
    const sharedNotes = notes.filter(n => selectedGroup.sharedNoteIds.includes(n.id));
    const sharedSets = flashcardSets.filter(s => selectedGroup.sharedSetIds.includes(s.id));

    return (
      <div id="group-detail-view" className="space-y-6 max-w-5xl mx-auto pb-12">
        {/* Header Back & Action */}
        <div className="flex items-center justify-between">
          <button
            id="back-to-groups-btn"
            onClick={() => setSelectedGroup(null)}
            className="flex items-center gap-2 text-xs font-semibold text-slate-600 hover:text-slate-900 bg-white border border-slate-200 px-3.5 py-1.5 rounded-xl shadow-2xs transition-colors"
          >
            <ArrowLeft className="w-4 h-4" />
            <span>Quay lại danh sách nhóm</span>
          </button>

          <button
            onClick={() => handleToggleJoinGroup(selectedGroup)}
            className={`px-4 py-1.5 text-xs font-semibold rounded-xl border transition-colors ${
              selectedGroup.isMember
                ? 'bg-slate-100 text-slate-700 hover:bg-rose-50 hover:text-rose-700 hover:border-rose-200'
                : 'bg-indigo-600 text-white hover:bg-indigo-700'
            }`}
          >
            {selectedGroup.isMember ? 'Đã tham gia (Rời nhóm)' : '+ Tham gia nhóm'}
          </button>
        </div>

        {/* Group Banner / Info Card */}
        <div className="bg-white rounded-2xl border border-slate-200 p-6 shadow-2xs">
          <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4">
            <div className="flex items-start gap-4">
              <div className="w-14 h-14 rounded-2xl bg-indigo-600 text-white flex items-center justify-center font-bold text-xl shadow-sm shrink-0">
                <Users className="w-7 h-7" />
              </div>
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <span className={`text-[10px] font-bold px-2 py-0.5 rounded-md ${color.bg} ${color.text} border ${color.border}`}>
                    {selectedGroup.subject}
                  </span>
                  <span className="text-xs text-slate-500 font-medium">
                    {selectedGroup.memberCount} thành viên
                  </span>
                  <span className="text-xs text-slate-400">• Trưởng nhóm: {selectedGroup.ownerName}</span>
                </div>
                <h2 className="text-lg sm:text-xl font-bold text-slate-900 leading-tight">
                  {selectedGroup.name}
                </h2>
                <p className="text-xs text-slate-600 mt-1.5 max-w-2xl leading-relaxed">
                  {selectedGroup.description}
                </p>
              </div>
            </div>
          </div>

          {/* Group Tabs Bar */}
          <div className="mt-6 pt-4 border-t border-slate-100 flex items-center gap-2 overflow-x-auto pb-1 sm:pb-0">
            <button
              id="group-tab-announcements"
              onClick={() => setDetailTab('announcements')}
              className={`px-4 py-2 text-xs font-bold rounded-xl transition-all flex items-center gap-2 ${
                detailTab === 'announcements'
                  ? 'bg-indigo-600 text-white shadow-2xs'
                  : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
              }`}
            >
              <Megaphone className="w-3.5 h-3.5" />
              <span>Bảng tin & Thông báo ({selectedGroup.announcements.length})</span>
            </button>
            <button
              id="group-tab-notes"
              onClick={() => setDetailTab('notes')}
              className={`px-4 py-2 text-xs font-bold rounded-xl transition-all flex items-center gap-2 ${
                detailTab === 'notes'
                  ? 'bg-indigo-600 text-white shadow-2xs'
                  : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
              }`}
            >
              <FileText className="w-3.5 h-3.5" />
              <span>Tài liệu chung ({sharedNotes.length})</span>
            </button>
            <button
              id="group-tab-flashcards"
              onClick={() => setDetailTab('flashcards')}
              className={`px-4 py-2 text-xs font-bold rounded-xl transition-all flex items-center gap-2 ${
                detailTab === 'flashcards'
                  ? 'bg-indigo-600 text-white shadow-2xs'
                  : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
              }`}
            >
              <Layers className="w-3.5 h-3.5" />
              <span>Flashcards ({sharedSets.length})</span>
            </button>
            <button
              id="group-tab-members"
              onClick={() => setDetailTab('members')}
              className={`px-4 py-2 text-xs font-bold rounded-xl transition-all flex items-center gap-2 ${
                detailTab === 'members'
                  ? 'bg-indigo-600 text-white shadow-2xs'
                  : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
              }`}
            >
              <Users className="w-3.5 h-3.5" />
              <span>Thành viên ({selectedGroup.members.length})</span>
            </button>
          </div>
        </div>

        {/* Tab 1: Announcements */}
        {detailTab === 'announcements' && (
          <div className="space-y-4">
            <div className="flex items-center justify-between bg-white p-4 rounded-2xl border border-slate-200 shadow-2xs">
              <div>
                <h3 className="text-sm font-bold text-slate-900">Bảng tin thảo luận nhóm</h3>
                <p className="text-xs text-slate-500">Thông báo bài tập, lịch ôn tập hoặc tài liệu mới</p>
              </div>
              <button
                id="create-announcement-btn"
                onClick={() => setIsNewAnnouncementModalOpen(true)}
                className="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs transition-colors flex items-center gap-1.5"
              >
                <Plus className="w-4 h-4" />
                <span>Đăng thông báo</span>
              </button>
            </div>

            <div className="space-y-3">
              {selectedGroup.announcements.map((anc) => (
                <div key={anc.id} className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs">
                  <div className="flex items-center justify-between pb-3 border-b border-slate-100">
                    <div className="flex items-center gap-2.5">
                      <img src={anc.authorAvatar} alt={anc.author} className="w-8 h-8 rounded-full object-cover" />
                      <div>
                        <p className="text-xs font-bold text-slate-900">{anc.author}</p>
                        <p className="text-[10px] text-slate-400">{anc.date}</p>
                      </div>
                    </div>
                    <span className="px-2 py-0.5 text-[10px] font-semibold bg-indigo-50 text-indigo-700 rounded-md">
                      Thông báo
                    </span>
                  </div>
                  <h4 className="text-sm font-bold text-slate-900 mt-3">{anc.title}</h4>
                  <p className="text-xs text-slate-600 mt-1 leading-relaxed whitespace-pre-line">
                    {anc.content}
                  </p>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Tab 2: Shared Notes */}
        {detailTab === 'notes' && (
          <div className="space-y-4">
            <div className="flex items-center justify-between bg-white p-4 rounded-2xl border border-slate-200 shadow-2xs">
              <div>
                <h3 className="text-sm font-bold text-slate-900">Ghi chú & Tài liệu học tập đã chia sẻ</h3>
                <p className="text-xs text-slate-500">Tất cả thành viên trong nhóm có thể đọc và tham khảo</p>
              </div>
              <button
                id="share-note-to-group-btn"
                onClick={() => setIsShareNoteModalOpen(true)}
                className="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs flex items-center gap-1.5"
              >
                <Plus className="w-4 h-4" />
                <span>Chia sẻ ghi chú vào nhóm</span>
              </button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {sharedNotes.map((note) => (
                <div key={note.id} className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs hover:border-indigo-300 transition-all flex flex-col justify-between">
                  <div>
                    <div className="flex items-center justify-between mb-2">
                      <span className="text-[10px] font-bold px-2 py-0.5 bg-indigo-50 text-indigo-700 rounded-md border border-indigo-200">
                        {note.subject}
                      </span>
                      <span className="text-[11px] text-slate-400">{note.updatedAt.slice(0, 10)}</span>
                    </div>
                    <h4 className="text-xs font-bold text-slate-900 line-clamp-1">{note.title}</h4>
                    <p className="text-[11px] text-slate-500 mt-1 line-clamp-2">{note.content.replace(/[#*$`]/g, '')}</p>
                  </div>
                  <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between">
                    <span className="text-[11px] text-slate-400">Tài liệu đã duyệt</span>
                    <button
                      onClick={() => onOpenNote(note.id)}
                      className="text-xs text-indigo-600 hover:text-indigo-800 font-semibold"
                    >
                      Mở xem chi tiết →
                    </button>
                  </div>
                </div>
              ))}
              {sharedNotes.length === 0 && (
                <div className="col-span-2 p-8 bg-white rounded-2xl border border-slate-200 text-center text-xs text-slate-500">
                  Chưa có ghi chú nào được chia sẻ trong nhóm này.
                </div>
              )}
            </div>
          </div>
        )}

        {/* Tab 3: Shared Flashcards */}
        {detailTab === 'flashcards' && (
          <div className="space-y-4">
            <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-2xs">
              <h3 className="text-sm font-bold text-slate-900">Bộ thẻ Flashcard của nhóm</h3>
              <p className="text-xs text-slate-500">Cùng luyện tập với các bộ thẻ do thành viên nhóm biên soạn</p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {sharedSets.map((set) => (
                <div key={set.id} className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs flex items-center justify-between gap-3">
                  <div>
                    <span className="text-[10px] font-bold px-2 py-0.5 bg-emerald-50 text-emerald-700 rounded-md border border-emerald-200">
                      {set.subject}
                    </span>
                    <h4 className="text-xs font-bold text-slate-900 mt-1.5">{set.name}</h4>
                    <p className="text-[11px] text-slate-500 mt-0.5">{set.cards.length} thẻ ôn tập</p>
                  </div>
                  <button
                    onClick={() => onOpenStudySet(set.id)}
                    className="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shrink-0"
                  >
                    Học ngay
                  </button>
                </div>
              ))}
              {sharedSets.length === 0 && (
                <div className="col-span-2 p-8 bg-white rounded-2xl border border-slate-200 text-center text-xs text-slate-500">
                  Chưa có bộ Flashcard nào được liên kết với nhóm này.
                </div>
              )}
            </div>
          </div>
        )}

        {/* Tab 4: Members List */}
        {detailTab === 'members' && (
          <div className="bg-white rounded-2xl border border-slate-200 p-6 shadow-2xs">
            <div className="pb-3 border-b border-slate-100 flex items-center justify-between">
              <div>
                <h3 className="text-sm font-bold text-slate-900">Danh sách thành viên ({selectedGroup.members.length})</h3>
                <p className="text-xs text-slate-500">Các bạn học sinh cùng sinh hoạt trong nhóm</p>
              </div>
            </div>

            <div className="mt-4 divide-y divide-slate-100">
              {selectedGroup.members.map((member) => (
                <div key={member.id} className="py-3 flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <img src={member.avatar} alt={member.name} className="w-10 h-10 rounded-xl object-cover border border-slate-200" />
                    <div>
                      <p className="text-xs font-bold text-slate-900">{member.name}</p>
                      <p className="text-[11px] text-slate-500">{member.grade}</p>
                    </div>
                  </div>

                  <span className={`text-[11px] font-semibold px-2.5 py-1 rounded-lg ${
                    member.role === 'Trưởng nhóm'
                      ? 'bg-amber-100 text-amber-800 border border-amber-200'
                      : member.role === 'Phó nhóm'
                      ? 'bg-indigo-100 text-indigo-800 border border-indigo-200'
                      : 'bg-slate-100 text-slate-600'
                  }`}>
                    {member.role}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Modal: New Announcement */}
        <Modal
          isOpen={isNewAnnouncementModalOpen}
          onClose={() => setIsNewAnnouncementModalOpen(false)}
          title="Đăng thông báo mới cho nhóm"
          subtitle={selectedGroup.name}
        >
          <form onSubmit={handlePostAnnouncement} className="space-y-4">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Tiêu đề thông báo</label>
              <input
                type="text"
                required
                placeholder="Ví dụ: Nhắc nhở lịch ôn tập vào tối Chủ Nhật..."
                value={announcementTitle}
                onChange={(e) => setAnnouncementTitle(e.target.value)}
                className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none focus:bg-white focus:border-indigo-500"
              />
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Nội dung chi tiết</label>
              <textarea
                required
                rows={4}
                placeholder="Nhập nội dung thông báo hoặc dặn dò các thành viên..."
                value={announcementContent}
                onChange={(e) => setAnnouncementContent(e.target.value)}
                className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none focus:bg-white focus:border-indigo-500"
              />
            </div>
            <div className="flex items-center justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setIsNewAnnouncementModalOpen(false)}
                className="px-4 py-2 text-xs font-semibold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl"
              >
                Hủy
              </button>
              <button
                type="submit"
                className="px-4 py-2 text-xs font-semibold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl shadow-2xs"
              >
                Đăng lên bảng tin
              </button>
            </div>
          </form>
        </Modal>

        {/* Modal: Share Note to Group */}
        <Modal
          isOpen={isShareNoteModalOpen}
          onClose={() => setIsShareNoteModalOpen(false)}
          title="Chia sẻ ghi chú vào nhóm"
          subtitle={selectedGroup.name}
          maxWidth="md"
        >
          <form onSubmit={handleShareNoteToGroup} className="space-y-4">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Chọn ghi chú từ kho của bạn</label>
              <select
                value={noteToShareId}
                onChange={(e) => setNoteToShareId(e.target.value)}
                className="w-full px-3 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none"
              >
                {notes.map((n) => (
                  <option key={n.id} value={n.id}>[{n.subject}] {n.title}</option>
                ))}
              </select>
            </div>
            <div className="flex items-center justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setIsShareNoteModalOpen(false)}
                className="px-4 py-2 text-xs font-semibold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl"
              >
                Hủy
              </button>
              <button
                type="submit"
                className="px-4 py-2 text-xs font-semibold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl shadow-2xs"
              >
                Chia sẻ ngay
              </button>
            </div>
          </form>
        </Modal>
      </div>
    );
  }

  // --- Render Mode: GROUPS LIST ---
  return (
    <div id="study-groups-view" className="space-y-6 max-w-7xl mx-auto pb-10">
      {/* Top Header & Filter Controls */}
      <div className="bg-white p-4 sm:p-5 rounded-2xl border border-slate-200 shadow-2xs flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-4">
        {/* Toggle 'My Groups' vs 'Explore All Groups' */}
        <div className="flex items-center gap-2 bg-slate-100 p-1 rounded-xl">
          <button
            id="tab-my-groups"
            onClick={() => setGroupViewFilter('my')}
            className={`px-4 py-2 text-xs font-bold rounded-lg transition-all ${
              groupViewFilter === 'my'
                ? 'bg-white text-indigo-600 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Nhóm của tôi ({groups.filter(g => g.isMember).length})
          </button>
          <button
            id="tab-all-groups"
            onClick={() => setGroupViewFilter('all')}
            className={`px-4 py-2 text-xs font-bold rounded-lg transition-all ${
              groupViewFilter === 'all'
                ? 'bg-white text-indigo-600 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Khám phá nhóm học ({groups.length})
          </button>
        </div>

        <button
          id="create-study-group-btn"
          onClick={() => {
            setIsCreateModalOpen(true);
            setNewGroupName('');
            setNewGroupDesc('');
          }}
          className="flex items-center justify-center gap-1.5 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs transition-colors shrink-0"
        >
          <Plus className="w-4 h-4" />
          <span>Tạo nhóm học tập mới</span>
        </button>
      </div>

      {/* Subject Filter & Search Bar */}
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
            Tất cả môn
          </button>
          {subjectsList.map((sub) => {
            const count = groups.filter(g => g.subject === sub).length;
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
            placeholder="Tìm tên nhóm học, môn học..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full pl-8 pr-3 py-1.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-800 focus:bg-white focus:border-indigo-400 outline-none"
          />
        </div>
      </div>

      {/* Groups Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
        {filteredGroups.map((grp) => {
          const color = subjectColorMap[grp.subject] || subjectColorMap['Toán'];
          return (
            <div
              key={grp.id}
              id={`group-card-${grp.id}`}
              onClick={() => setSelectedGroup(grp)}
              className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs hover:shadow-md hover:border-indigo-300 transition-all flex flex-col justify-between cursor-pointer"
            >
              <div>
                <div className="flex items-center justify-between gap-2 mb-3">
                  <span className={`text-[10px] font-bold px-2 py-0.5 rounded-md ${color.bg} ${color.text} border ${color.border}`}>
                    {grp.subject}
                  </span>

                  <span className="text-xs text-slate-500 font-medium flex items-center gap-1">
                    <Users className="w-3.5 h-3.5 text-slate-400" />
                    <span>{grp.memberCount} thành viên</span>
                  </span>
                </div>

                <h3 className="text-sm font-bold text-slate-900 leading-snug">
                  {grp.name}
                </h3>
                <p className="text-xs text-slate-500 mt-1.5 line-clamp-2 leading-relaxed">
                  {grp.description}
                </p>

                <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-[11px] text-slate-500">
                  <span>Trưởng nhóm: <strong>{grp.ownerName}</strong></span>
                  <span>{grp.announcements.length} thông báo</span>
                </div>
              </div>

              <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between gap-2">
                <button
                  type="button"
                  onClick={(e) => handleToggleJoinGroup(grp, e)}
                  className={`text-xs font-semibold px-3 py-1.5 rounded-xl border transition-colors ${
                    grp.isMember
                      ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                      : 'bg-slate-100 text-slate-700 border-slate-200 hover:bg-slate-200'
                  }`}
                >
                  {grp.isMember ? '✓ Đã tham gia' : '+ Tham gia'}
                </button>

                <button
                  type="button"
                  onClick={() => setSelectedGroup(grp)}
                  className="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs transition-colors"
                >
                  Vào phòng nhóm
                </button>
              </div>
            </div>
          );
        })}
      </div>

      {filteredGroups.length === 0 && (
        <div className="bg-white rounded-2xl border border-slate-200 p-12 text-center">
          <Users className="w-12 h-12 text-slate-300 mx-auto mb-3" />
          <h3 className="text-sm font-bold text-slate-800">Không tìm thấy nhóm học nào</h3>
          <p className="text-xs text-slate-500 mt-1 max-w-sm mx-auto">
            {groupViewFilter === 'my'
              ? 'Bạn chưa tham gia nhóm nào trong danh mục này. Hãy duyệt mục "Khám phá nhóm" để tham gia cùng bạn bè.'
              : 'Chưa có nhóm nào phù hợp với từ khóa tìm kiếm.'}
          </p>
          <button
            onClick={() => setIsCreateModalOpen(true)}
            className="mt-4 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs"
          >
            + Tạo nhóm đầu tiên
          </button>
        </div>
      )}

      {/* Modal Create Group */}
      <Modal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        title="Tạo nhóm học tập mới"
        subtitle="Cùng bạn bè chia sẻ tài liệu và ôn luyện bài học"
      >
        <form onSubmit={handleCreateGroupSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">Tên nhóm học</label>
            <input
              type="text"
              required
              placeholder="Ví dụ: Ôn thi HSG Toán 11, CLB Tiếng Anh Amsterdam..."
              value={newGroupName}
              onChange={(e) => setNewGroupName(e.target.value)}
              className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none focus:bg-white focus:border-indigo-500"
            />
          </div>

          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">Môn học trọng tâm</label>
            <select
              value={newGroupSubject}
              onChange={(e) => setNewGroupSubject(e.target.value as Subject)}
              className="w-full px-3 py-2 text-xs text-slate-800 bg-slate-50 border border-slate-300 rounded-xl outline-none"
            >
              {subjectsList.map((sub) => (
                <option key={sub} value={sub}>{sub}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">Mô tả mục tiêu nhóm</label>
            <textarea
              rows={3}
              placeholder="Mô tả ngắn về kế hoạch học tập, mục tiêu điểm số và quy định tham gia..."
              value={newGroupDesc}
              onChange={(e) => setNewGroupDesc(e.target.value)}
              className="w-full px-3.5 py-2 text-xs text-slate-900 bg-slate-50 border border-slate-300 rounded-xl outline-none focus:bg-white focus:border-indigo-500"
            />
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
            <button
              type="button"
              onClick={() => setIsCreateModalOpen(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl"
            >
              Hủy
            </button>
            <button
              type="submit"
              className="px-4 py-2 text-xs font-semibold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl shadow-2xs"
            >
              Tạo nhóm ngay
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
};
