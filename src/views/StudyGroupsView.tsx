import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  ArrowLeft,
  BookOpen,
  CalendarClock,
  Check,
  Clock3,
  Copy,
  ExternalLink,
  FileText,
  Globe2,
  Heart,
  Layers,
  Lock,
  Megaphone,
  MessageCircle,
  Pencil,
  Plus,
  Search,
  Send,
  Trash2,
  UserCheck,
  UserPlus,
  Users,
  Video,
  X,
} from 'lucide-react';
import {
  FlashcardSet,
  GroupJoinRequest,
  Note,
  StudyGroup,
  StudyMode,
  Subject,
  UserProfile,
} from '../types';
import { subjectColorMap } from '../mockData';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { Modal } from '../components/Modal';

interface StudyGroupsViewProps {
  user: UserProfile;
  groups: StudyGroup[];
  notes: Note[];
  flashcardSets: FlashcardSet[];
  onSaveGroup: (group: StudyGroup) => void;
  onDeleteGroup: (groupId: string) => void;
  showToast: (msg: string, type?: 'success' | 'error' | 'info') => void;
  onOpenNote: (noteId: string) => void;
  onOpenStudySet: (setId: string) => void;
  createRequestKey?: number;
}

type GroupFilter = 'my' | 'discover' | 'saved';
type DetailTab = 'announcements' | 'chat' | 'notes' | 'flashcards' | 'members';
type ResourceType = 'note' | 'flashcard';

interface GroupFormState {
  name: string;
  subject: Subject;
  description: string;
  goal: string;
  capacity: number;
  studyMode: StudyMode;
  meetingTime: string;
  contactLink: string;
  isPublic: boolean;
}

const subjectsList: Subject[] = [
  'Toán',
  'Vật lý',
  'Hóa học',
  'Ngữ văn',
  'Tiếng Anh',
  'Sinh học',
  'Lịch sử',
  'Địa lý',
  'Tin học',
  'GDCD',
];

const emptyForm: GroupFormState = {
  name: '',
  subject: 'Toán',
  description: '',
  goal: '',
  capacity: 6,
  studyMode: 'Online',
  meetingTime: '',
  contactLink: '',
  isPublic: true,
};

const isSafeContact = (value: string) =>
  value === '' || /^(https?:\/\/|mailto:)[^\s]+$/i.test(value);

const memberCapacity = (group: StudyGroup) => Math.max(group.capacity ?? 20, group.memberCount);

const getPendingRequest = (group: StudyGroup, userId: string) =>
  (group.joinRequests ?? []).find(
    (request) => request.userId === userId && request.status === 'pending',
  );

export const StudyGroupsView: React.FC<StudyGroupsViewProps> = ({
  user,
  groups,
  notes,
  flashcardSets,
  onSaveGroup,
  onDeleteGroup,
  showToast,
  onOpenNote,
  onOpenStudySet,
  createRequestKey = 0,
}) => {
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);
  const [detailTab, setDetailTab] = useState<DetailTab>('announcements');
  const [groupFilter, setGroupFilter] = useState<GroupFilter>('my');
  const [subjectFilter, setSubjectFilter] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');

  const [isGroupModalOpen, setIsGroupModalOpen] = useState(false);
  const [editingGroupId, setEditingGroupId] = useState<string | null>(null);
  const [groupForm, setGroupForm] = useState<GroupFormState>(emptyForm);

  const [joinTargetId, setJoinTargetId] = useState<string | null>(null);
  const [joinMessage, setJoinMessage] = useState('');
  const [deleteTargetId, setDeleteTargetId] = useState<string | null>(null);

  const [isAnnouncementModalOpen, setIsAnnouncementModalOpen] = useState(false);
  const [announcementTitle, setAnnouncementTitle] = useState('');
  const [announcementContent, setAnnouncementContent] = useState('');

  const [resourceType, setResourceType] = useState<ResourceType>('note');
  const [resourceId, setResourceId] = useState('');
  const [isResourceModalOpen, setIsResourceModalOpen] = useState(false);
  const [chatMessage, setChatMessage] = useState('');
  const chatEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (createRequestKey > 0) {
      setEditingGroupId(null);
      setGroupForm(emptyForm);
      setIsGroupModalOpen(true);
    }
  }, [createRequestKey]);

  const selectedGroup = selectedGroupId
    ? groups.find((group) => group.id === selectedGroupId) ?? null
    : null;

  useEffect(() => {
    if (detailTab === 'chat') {
      chatEndRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' });
    }
  }, [detailTab, selectedGroup?.messages?.length]);

  const isOwner = (group: StudyGroup) =>
    group.ownerId ? group.ownerId === user.id : group.ownerName === user.name;

  const isGroupMember = (group: StudyGroup) => Boolean(
    group.isMember
    || isOwner(group)
    || group.members.some((member) => member.id === user.id || member.name === user.name),
  );

  const filteredGroups = useMemo(() => {
    const normalizedQuery = searchQuery.trim().toLocaleLowerCase('vi');
    return groups.filter((group) => {
      const matchesVisibility = group.isPublic !== false || isGroupMember(group);
      const matchesSection =
        groupFilter === 'my'
          ? isGroupMember(group)
          : groupFilter === 'saved'
            ? Boolean(group.isSaved)
            : !isGroupMember(group);
      const matchesSubject = subjectFilter === 'all' || group.subject === subjectFilter;
      const haystack = `${group.name} ${group.description} ${group.goal ?? ''} ${group.subject}`
        .toLocaleLowerCase('vi');
      return matchesVisibility && matchesSection && matchesSubject && haystack.includes(normalizedQuery);
    });
  }, [groups, groupFilter, searchQuery, subjectFilter, user.id, user.name]);

  const openCreateModal = () => {
    setEditingGroupId(null);
    setGroupForm(emptyForm);
    setIsGroupModalOpen(true);
  };

  const openEditModal = (group: StudyGroup) => {
    setEditingGroupId(group.id);
    setGroupForm({
      name: group.name,
      subject: group.subject,
      description: group.description,
      goal: group.goal ?? '',
      capacity: memberCapacity(group),
      studyMode: group.studyMode ?? 'Online',
      meetingTime: group.meetingTime ?? '',
      contactLink: group.contactLink ?? '',
      isPublic: group.isPublic !== false,
    });
    setIsGroupModalOpen(true);
  };

  const saveGroup = (event: React.FormEvent) => {
    event.preventDefault();
    const name = groupForm.name.trim();
    const description = groupForm.description.trim();
    const goal = groupForm.goal.trim();
    const contactLink = groupForm.contactLink.trim();

    if (name.length < 5) {
      showToast('Tên nhóm cần có ít nhất 5 ký tự.', 'error');
      return;
    }
    if (description.length < 10 || goal.length < 5) {
      showToast('Hãy mô tả rõ hoạt động và mục tiêu của nhóm.', 'error');
      return;
    }
    if (groupForm.capacity < 2 || groupForm.capacity > 100) {
      showToast('Số thành viên phải từ 2 đến 100.', 'error');
      return;
    }
    if (!isSafeContact(contactLink)) {
      showToast('Liên kết liên hệ phải bắt đầu bằng http://, https:// hoặc mailto:.', 'error');
      return;
    }

    const existing = editingGroupId
      ? groups.find((group) => group.id === editingGroupId)
      : undefined;
    if (existing && groupForm.capacity < existing.memberCount) {
      showToast('Sức chứa không thể nhỏ hơn số thành viên hiện tại.', 'error');
      return;
    }

    const now = new Date().toISOString();
    const savedGroup: StudyGroup = existing
      ? {
          ...existing,
          name,
          subject: groupForm.subject,
          description,
          goal,
          capacity: groupForm.capacity,
          studyMode: groupForm.studyMode,
          meetingTime: groupForm.meetingTime.trim(),
          contactLink,
          isPublic: groupForm.isPublic,
        }
      : {
          id: `grp-${Date.now()}`,
          name,
          subject: groupForm.subject,
          description,
          goal,
          ownerId: user.id,
          ownerName: user.name,
          memberCount: 1,
          capacity: groupForm.capacity,
          studyMode: groupForm.studyMode,
          meetingTime: groupForm.meetingTime.trim(),
          contactLink,
          isPublic: groupForm.isPublic,
          isSaved: true,
          createdAt: now,
          isMember: true,
          members: [
            {
              id: user.id,
              name: user.name,
              role: 'Trưởng nhóm',
              avatar: user.avatarUrl,
              grade: user.grade,
            },
          ],
          announcements: [
            {
              id: `anc-${Date.now()}`,
              author: user.name,
              authorAvatar: user.avatarUrl,
              date: now,
              title: `Chào mừng đến với ${name}`,
              content: 'Nhóm đã được tạo. Hãy cùng chia sẻ tài liệu và lên kế hoạch học tập nhé!',
            },
          ],
          messages: [
            {
              id: `msg-${Date.now()}`,
              authorId: user.id,
              authorName: user.name,
              authorAvatar: user.avatarUrl,
              content: `Chào mừng mọi người đến với nhóm ${name}!`,
              sentAt: now,
            },
          ],
          sharedNoteIds: [],
          sharedSetIds: [],
          joinRequests: [],
        };

    onSaveGroup(savedGroup);
    setSelectedGroupId(savedGroup.id);
    setIsGroupModalOpen(false);
    showToast(existing ? 'Đã cập nhật thông tin nhóm.' : 'Đã tạo nhóm học tập mới.', 'success');
  };

  const toggleSaved = (group: StudyGroup, event?: React.MouseEvent) => {
    event?.stopPropagation();
    onSaveGroup({ ...group, isSaved: !group.isSaved });
    showToast(group.isSaved ? 'Đã bỏ nhóm khỏi danh sách lưu.' : 'Đã lưu nhóm để xem sau.', 'info');
  };

  const submitJoinRequest = (event: React.FormEvent) => {
    event.preventDefault();
    const group = groups.find((item) => item.id === joinTargetId);
    if (!group) return;
    if (group.memberCount >= memberCapacity(group)) {
      showToast('Nhóm đã đủ thành viên.', 'error');
      return;
    }
    if (getPendingRequest(group, user.id)) {
      showToast('Yêu cầu của bạn đang chờ trưởng nhóm duyệt.', 'info');
      return;
    }

    const request: GroupJoinRequest = {
      id: `req-${Date.now()}`,
      userId: user.id,
      userName: user.name,
      userAvatar: user.avatarUrl,
      grade: user.grade,
      message: joinMessage.trim() || 'Mình muốn tham gia và cùng học với nhóm.',
      requestedAt: new Date().toISOString(),
      status: 'pending',
    };
    onSaveGroup({ ...group, joinRequests: [request, ...(group.joinRequests ?? [])] });
    setJoinTargetId(null);
    setJoinMessage('');
    showToast('Đã gửi yêu cầu tham gia đến trưởng nhóm.', 'success');
  };

  const cancelJoinRequest = (group: StudyGroup) => {
    onSaveGroup({
      ...group,
      joinRequests: (group.joinRequests ?? []).filter(
        (request) => !(request.userId === user.id && request.status === 'pending'),
      ),
    });
    showToast('Đã hủy yêu cầu tham gia.', 'info');
  };

  const reviewRequest = (group: StudyGroup, request: GroupJoinRequest, accept: boolean) => {
    if (!isOwner(group)) return;
    if (accept && group.memberCount >= memberCapacity(group)) {
      showToast('Nhóm đã đủ thành viên, không thể duyệt thêm.', 'error');
      return;
    }

    const updatedRequests = (group.joinRequests ?? []).map((item) =>
      item.id === request.id ? { ...item, status: accept ? 'accepted' as const : 'rejected' as const } : item,
    );
    const alreadyMember = group.members.some(
      (member) => member.id === request.userId || member.name === request.userName,
    );
    const updatedMembers = accept && !alreadyMember
      ? [
          ...group.members,
          {
            id: request.userId,
            name: request.userName,
            role: 'Thành viên' as const,
            avatar: request.userAvatar,
            grade: request.grade,
          },
        ]
      : group.members;

    onSaveGroup({
      ...group,
      joinRequests: updatedRequests,
      members: updatedMembers,
      memberCount: accept && !alreadyMember ? group.memberCount + 1 : group.memberCount,
    });
    showToast(accept ? `Đã duyệt ${request.userName} vào nhóm.` : `Đã từ chối yêu cầu của ${request.userName}.`, accept ? 'success' : 'info');
  };

  const leaveGroup = (group: StudyGroup) => {
    if (isOwner(group)) {
      showToast('Trưởng nhóm cần xóa nhóm hoặc chuyển quyền trước khi rời.', 'error');
      return;
    }
    onSaveGroup({
      ...group,
      isMember: false,
      memberCount: Math.max(1, group.memberCount - 1),
      members: group.members.filter(
        (member) => member.id !== user.id && member.name !== user.name,
      ),
    });
    setSelectedGroupId(null);
    showToast(`Bạn đã rời nhóm ${group.name}.`, 'info');
  };

  const postAnnouncement = (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedGroup || !isGroupMember(selectedGroup)) return;
    if (!announcementTitle.trim() || !announcementContent.trim()) {
      showToast('Hãy nhập đủ tiêu đề và nội dung thông báo.', 'error');
      return;
    }
    onSaveGroup({
      ...selectedGroup,
      announcements: [
        {
          id: `anc-${Date.now()}`,
          author: user.name,
          authorAvatar: user.avatarUrl,
          date: new Date().toISOString(),
          title: announcementTitle.trim(),
          content: announcementContent.trim(),
        },
        ...selectedGroup.announcements,
      ],
    });
    setAnnouncementTitle('');
    setAnnouncementContent('');
    setIsAnnouncementModalOpen(false);
    showToast('Đã đăng thông báo vào bảng tin nhóm.', 'success');
  };

  const sendChatMessage = (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedGroup || !isGroupMember(selectedGroup)) return;
    const content = chatMessage.trim();
    if (!content) return;
    if (content.length > 1000) {
      showToast('Tin nhắn không được dài quá 1000 ký tự.', 'error');
      return;
    }

    onSaveGroup({
      ...selectedGroup,
      messages: [
        ...(selectedGroup.messages ?? []),
        {
          id: `msg-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
          authorId: user.id,
          authorName: user.name,
          authorAvatar: user.avatarUrl,
          content,
          sentAt: new Date().toISOString(),
        },
      ],
    });
    setChatMessage('');
  };

  const openResourceModal = (type: ResourceType) => {
    const resources = type === 'note' ? notes : flashcardSets;
    setResourceType(type);
    setResourceId(resources[0]?.id ?? '');
    setIsResourceModalOpen(true);
  };

  const shareResource = (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedGroup || !resourceId || !isGroupMember(selectedGroup)) return;
    const field = resourceType === 'note' ? 'sharedNoteIds' : 'sharedSetIds';
    if (selectedGroup[field].includes(resourceId)) {
      showToast('Tài liệu này đã có trong nhóm.', 'info');
      return;
    }
    onSaveGroup({ ...selectedGroup, [field]: [...selectedGroup[field], resourceId] });
    setIsResourceModalOpen(false);
    showToast('Đã chia sẻ tài liệu vào nhóm.', 'success');
  };

  const copyGroupLink = async (group: StudyGroup) => {
    try {
      await navigator.clipboard.writeText(`${window.location.origin}${window.location.pathname}#group=${group.id}`);
      showToast('Đã sao chép liên kết nhóm.', 'success');
    } catch {
      showToast('Trình duyệt không cho phép sao chép liên kết.', 'error');
    }
  };

  const confirmDelete = () => {
    if (!deleteTargetId) return;
    onDeleteGroup(deleteTargetId);
    if (selectedGroupId === deleteTargetId) setSelectedGroupId(null);
    setDeleteTargetId(null);
    showToast('Đã xóa nhóm học tập.', 'success');
  };

  if (selectedGroup) {
    const color = subjectColorMap[selectedGroup.subject] || subjectColorMap['Toán'];
    const sharedNotes = notes.filter((note) => selectedGroup.sharedNoteIds.includes(note.id));
    const sharedSets = flashcardSets.filter((set) => selectedGroup.sharedSetIds.includes(set.id));
    const owner = isOwner(selectedGroup);
    const member = isGroupMember(selectedGroup);
    const pendingRequest = getPendingRequest(selectedGroup, user.id);
    const pendingRequests = (selectedGroup.joinRequests ?? []).filter((request) => request.status === 'pending');
    const full = selectedGroup.memberCount >= memberCapacity(selectedGroup);

    return (
      <div id="group-detail-view" className="space-y-5 max-w-6xl mx-auto pb-12">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <button
            type="button"
            onClick={() => setSelectedGroupId(null)}
            className="flex items-center gap-2 text-xs font-semibold text-slate-600 hover:text-slate-900 bg-white border border-slate-200 px-3.5 py-2 rounded-xl"
          >
            <ArrowLeft className="w-4 h-4" /> Quay lại danh sách
          </button>
          <div className="flex items-center gap-2">
            <button type="button" onClick={() => toggleSaved(selectedGroup)} className="p-2.5 bg-white border border-slate-200 rounded-xl text-slate-600" title="Lưu nhóm">
              <Heart className={`w-4 h-4 ${selectedGroup.isSaved ? 'fill-rose-500 text-rose-500' : ''}`} />
            </button>
            <button type="button" onClick={() => copyGroupLink(selectedGroup)} className="p-2.5 bg-white border border-slate-200 rounded-xl text-slate-600" title="Sao chép liên kết">
              <Copy className="w-4 h-4" />
            </button>
            {owner && (
              <>
                <button type="button" onClick={() => openEditModal(selectedGroup)} className="flex items-center gap-1.5 px-3.5 py-2 bg-white border border-slate-200 rounded-xl text-xs font-semibold text-slate-700">
                  <Pencil className="w-3.5 h-3.5" /> Sửa
                </button>
                <button type="button" onClick={() => setDeleteTargetId(selectedGroup.id)} className="p-2.5 bg-rose-50 border border-rose-200 rounded-xl text-rose-600" title="Xóa nhóm">
                  <Trash2 className="w-4 h-4" />
                </button>
              </>
            )}
          </div>
        </div>

        <section className="bg-white rounded-2xl border border-slate-200 p-5 sm:p-6 shadow-2xs">
          <div className="flex flex-col lg:flex-row lg:items-start justify-between gap-5">
            <div className="flex items-start gap-4">
              <div className="w-14 h-14 rounded-2xl bg-indigo-600 text-white flex items-center justify-center shrink-0">
                <Users className="w-7 h-7" />
              </div>
              <div>
                <div className="flex flex-wrap items-center gap-2 mb-1.5">
                  <span className={`text-[10px] font-bold px-2 py-0.5 rounded-md ${color.bg} ${color.text} border ${color.border}`}>{selectedGroup.subject}</span>
                  <span className="text-[10px] px-2 py-0.5 rounded-md bg-slate-100 text-slate-600 flex items-center gap-1">
                    {selectedGroup.isPublic === false ? <Lock className="w-3 h-3" /> : <Globe2 className="w-3 h-3" />}
                    {selectedGroup.isPublic === false ? 'Nhóm riêng tư' : 'Nhóm công khai'}
                  </span>
                </div>
                <h1 className="text-xl sm:text-2xl font-bold text-slate-900">{selectedGroup.name}</h1>
                <p className="text-xs text-slate-600 mt-2 max-w-3xl leading-relaxed">{selectedGroup.description}</p>
              </div>
            </div>

            {!owner && (
              member ? (
                <button type="button" onClick={() => leaveGroup(selectedGroup)} className="px-4 py-2 text-xs font-semibold bg-slate-100 hover:bg-rose-50 text-slate-700 hover:text-rose-700 rounded-xl">Rời nhóm</button>
              ) : pendingRequest ? (
                <button type="button" onClick={() => cancelJoinRequest(selectedGroup)} className="px-4 py-2 text-xs font-semibold bg-amber-50 text-amber-700 border border-amber-200 rounded-xl">Đang chờ duyệt · Hủy</button>
              ) : (
                <button type="button" disabled={full} onClick={() => setJoinTargetId(selectedGroup.id)} className="px-4 py-2 text-xs font-semibold bg-indigo-600 disabled:bg-slate-300 text-white rounded-xl">
                  {full ? 'Nhóm đã đủ người' : 'Gửi yêu cầu tham gia'}
                </button>
              )
            )}
          </div>

          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 mt-6 pt-5 border-t border-slate-100">
            <InfoTile icon={<Users className="w-4 h-4" />} label="Thành viên" value={`${selectedGroup.memberCount}/${memberCapacity(selectedGroup)}`} />
            <InfoTile icon={<Video className="w-4 h-4" />} label="Hình thức" value={selectedGroup.studyMode ?? 'Chưa cập nhật'} />
            <InfoTile icon={<CalendarClock className="w-4 h-4" />} label="Lịch học" value={selectedGroup.meetingTime || 'Chưa cập nhật'} />
            <InfoTile icon={<UserCheck className="w-4 h-4" />} label="Trưởng nhóm" value={selectedGroup.ownerName} />
          </div>

          {selectedGroup.goal && (
            <div className="mt-4 p-3.5 bg-indigo-50 border border-indigo-100 rounded-xl">
              <p className="text-[10px] font-bold text-indigo-500 uppercase tracking-wider">Mục tiêu học tập</p>
              <p className="text-xs text-indigo-900 mt-1">{selectedGroup.goal}</p>
            </div>
          )}

          <div className="flex flex-wrap items-center gap-2 mt-4">
            {selectedGroup.contactLink && isSafeContact(selectedGroup.contactLink) && (
              <a href={selectedGroup.contactLink} target={selectedGroup.contactLink.startsWith('mailto:') ? undefined : '_blank'} rel="noreferrer" className="inline-flex items-center gap-1.5 px-3 py-2 bg-indigo-600 text-white text-xs font-semibold rounded-xl">
                <ExternalLink className="w-3.5 h-3.5" /> Liên hệ nhóm
              </a>
            )}
            <span className="text-[11px] text-slate-500">Chủ nhóm: {selectedGroup.ownerName}</span>
          </div>
        </section>

        {!member ? (
          <section className="bg-white rounded-2xl border border-slate-200 p-8 text-center">
            <Lock className="w-10 h-10 text-slate-300 mx-auto" />
            <h2 className="text-base font-bold text-slate-900 mt-3">Nội dung dành cho thành viên</h2>
            <p className="text-xs text-slate-500 mt-1 max-w-lg mx-auto">Gửi yêu cầu tham gia để xem bảng tin, tài liệu, flashcards và danh sách thành viên của nhóm.</p>
          </section>
        ) : (
          <>
            <div className="bg-white border border-slate-200 rounded-2xl p-2 flex gap-2 overflow-x-auto">
              <TabButton active={detailTab === 'announcements'} onClick={() => setDetailTab('announcements')} icon={<Megaphone className="w-3.5 h-3.5" />} label={`Bảng tin (${selectedGroup.announcements.length})`} />
              <TabButton active={detailTab === 'chat'} onClick={() => setDetailTab('chat')} icon={<MessageCircle className="w-3.5 h-3.5" />} label={`Chat (${selectedGroup.messages?.length ?? 0})`} />
              <TabButton active={detailTab === 'notes'} onClick={() => setDetailTab('notes')} icon={<FileText className="w-3.5 h-3.5" />} label={`Ghi chú (${sharedNotes.length})`} />
              <TabButton active={detailTab === 'flashcards'} onClick={() => setDetailTab('flashcards')} icon={<Layers className="w-3.5 h-3.5" />} label={`Flashcards (${sharedSets.length})`} />
              <TabButton active={detailTab === 'members'} onClick={() => setDetailTab('members')} icon={<Users className="w-3.5 h-3.5" />} label={`Thành viên (${selectedGroup.members.length})`} />
            </div>

            {detailTab === 'announcements' && (
              <section className="space-y-3">
                <SectionHeader title="Bảng tin nhóm" subtitle="Thông báo lịch học, nhiệm vụ và tài liệu mới">
                  <button type="button" onClick={() => setIsAnnouncementModalOpen(true)} className="inline-flex items-center gap-1.5 px-3.5 py-2 bg-indigo-600 text-white text-xs font-semibold rounded-xl"><Plus className="w-4 h-4" /> Đăng thông báo</button>
                </SectionHeader>
                {selectedGroup.announcements.map((announcement) => (
                  <article key={announcement.id} className="bg-white rounded-2xl border border-slate-200 p-5">
                    <div className="flex items-center gap-2.5 pb-3 border-b border-slate-100">
                      <img src={announcement.authorAvatar} alt="" className="w-9 h-9 rounded-full object-cover" />
                      <div><p className="text-xs font-bold text-slate-900">{announcement.author}</p><p className="text-[10px] text-slate-400">{formatDate(announcement.date)}</p></div>
                    </div>
                    <h3 className="text-sm font-bold text-slate-900 mt-3">{announcement.title}</h3>
                    <p className="text-xs text-slate-600 mt-1 whitespace-pre-line leading-relaxed">{announcement.content}</p>
                  </article>
                ))}
                {selectedGroup.announcements.length === 0 && <EmptyState text="Chưa có thông báo trong nhóm." />}
              </section>
            )}

            {detailTab === 'chat' && (
              <section className="bg-white rounded-2xl border border-slate-200 overflow-hidden shadow-2xs">
                <div className="px-4 sm:px-5 py-3.5 border-b border-slate-100 flex items-center justify-between gap-3">
                  <div>
                    <h2 className="text-sm font-bold text-slate-900 flex items-center gap-2"><MessageCircle className="w-4 h-4 text-indigo-600" /> Chat nhóm</h2>
                    <p className="text-[11px] text-slate-500 mt-0.5">Chỉ thành viên của nhóm có thể đọc và gửi tin nhắn.</p>
                  </div>
                  <span className="text-[10px] font-semibold text-emerald-700 bg-emerald-50 px-2.5 py-1 rounded-full">{selectedGroup.memberCount} thành viên</span>
                </div>

                <div className="h-[420px] overflow-y-auto bg-slate-50/70 p-4 sm:p-5 space-y-4" aria-live="polite">
                  {(selectedGroup.messages ?? []).map((message) => {
                    const isMine = message.authorId === user.id || message.authorName === user.name;
                    return (
                      <div key={message.id} className={`flex items-end gap-2.5 ${isMine ? 'justify-end' : 'justify-start'}`}>
                        {!isMine && <img src={message.authorAvatar} alt="" className="w-8 h-8 rounded-full object-cover shrink-0 border border-white shadow-sm" />}
                        <div className={`max-w-[82%] sm:max-w-[70%] ${isMine ? 'items-end' : 'items-start'} flex flex-col`}>
                          {!isMine && <span className="text-[10px] font-semibold text-slate-500 mb-1 ml-1">{message.authorName}</span>}
                          <div className={`px-3.5 py-2.5 rounded-2xl text-xs leading-relaxed whitespace-pre-wrap break-words ${isMine ? 'bg-indigo-600 text-white rounded-br-md' : 'bg-white text-slate-800 border border-slate-200 rounded-bl-md'}`}>
                            {message.content}
                          </div>
                          <time className="text-[9px] text-slate-400 mt-1 mx-1" dateTime={message.sentAt}>{formatChatTime(message.sentAt)}</time>
                        </div>
                        {isMine && <img src={message.authorAvatar} alt="" className="w-8 h-8 rounded-full object-cover shrink-0 border border-white shadow-sm" />}
                      </div>
                    );
                  })}
                  {(selectedGroup.messages?.length ?? 0) === 0 && (
                    <div className="h-full flex flex-col items-center justify-center text-center">
                      <MessageCircle className="w-10 h-10 text-slate-300" />
                      <p className="text-xs font-semibold text-slate-600 mt-3">Chưa có tin nhắn</p>
                      <p className="text-[11px] text-slate-400 mt-1">Hãy bắt đầu cuộc trò chuyện với nhóm.</p>
                    </div>
                  )}
                  <div ref={chatEndRef} />
                </div>

                <form onSubmit={sendChatMessage} className="p-3 sm:p-4 border-t border-slate-100 bg-white">
                  <div className="flex items-end gap-2">
                    <textarea
                      value={chatMessage}
                      onChange={(event) => setChatMessage(event.target.value)}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter' && !event.shiftKey) {
                          event.preventDefault();
                          event.currentTarget.form?.requestSubmit();
                        }
                      }}
                      rows={2}
                      maxLength={1000}
                      placeholder="Nhập tin nhắn… (Enter để gửi, Shift + Enter để xuống dòng)"
                      className="form-input resize-none min-h-[44px] max-h-32"
                      aria-label="Nội dung tin nhắn"
                    />
                    <button type="submit" disabled={!chatMessage.trim()} className="h-11 px-4 bg-indigo-600 hover:bg-indigo-700 disabled:bg-slate-300 text-white rounded-xl inline-flex items-center gap-1.5 text-xs font-semibold shrink-0">
                      <Send className="w-4 h-4" /><span className="hidden sm:inline">Gửi</span>
                    </button>
                  </div>
                  <div className="text-right text-[9px] text-slate-400 mt-1">{chatMessage.length}/1000</div>
                </form>
              </section>
            )}

            {detailTab === 'notes' && (
              <section className="space-y-3">
                <SectionHeader title="Ghi chú được chia sẻ" subtitle="Thành viên có thể mở ghi chú trong kho học tập">
                  <button type="button" onClick={() => openResourceModal('note')} disabled={notes.length === 0} className="inline-flex items-center gap-1.5 px-3.5 py-2 bg-indigo-600 disabled:bg-slate-300 text-white text-xs font-semibold rounded-xl"><Plus className="w-4 h-4" /> Chia sẻ ghi chú</button>
                </SectionHeader>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {sharedNotes.map((note) => (
                    <ResourceCard key={note.id} badge={note.subject} title={note.title} description={note.content.replace(/[#*$`]/g, '').slice(0, 150)} action="Mở ghi chú" onClick={() => onOpenNote(note.id)} />
                  ))}
                </div>
                {sharedNotes.length === 0 && <EmptyState text="Chưa có ghi chú nào được chia sẻ." />}
              </section>
            )}

            {detailTab === 'flashcards' && (
              <section className="space-y-3">
                <SectionHeader title="Flashcards của nhóm" subtitle="Cùng luyện tập bằng các bộ thẻ của thành viên">
                  <button type="button" onClick={() => openResourceModal('flashcard')} disabled={flashcardSets.length === 0} className="inline-flex items-center gap-1.5 px-3.5 py-2 bg-indigo-600 disabled:bg-slate-300 text-white text-xs font-semibold rounded-xl"><Plus className="w-4 h-4" /> Chia sẻ bộ thẻ</button>
                </SectionHeader>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {sharedSets.map((set) => (
                    <ResourceCard key={set.id} badge={set.subject} title={set.name} description={`${set.cards.length} thẻ · ${set.description}`} action="Học ngay" onClick={() => onOpenStudySet(set.id)} />
                  ))}
                </div>
                {sharedSets.length === 0 && <EmptyState text="Chưa có bộ flashcard nào được chia sẻ." />}
              </section>
            )}

            {detailTab === 'members' && (
              <section className="space-y-4">
                {owner && (
                  <div className="bg-white rounded-2xl border border-slate-200 p-5">
                    <h2 className="text-sm font-bold text-slate-900">Yêu cầu tham gia ({pendingRequests.length})</h2>
                    <p className="text-xs text-slate-500 mt-1">Duyệt thành viên phù hợp với mục tiêu của nhóm.</p>
                    <div className="mt-3 divide-y divide-slate-100">
                      {pendingRequests.map((request) => (
                        <div key={request.id} className="py-3 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                          <div className="flex items-center gap-3">
                            <img src={request.userAvatar} alt="" className="w-10 h-10 rounded-xl object-cover" />
                            <div><p className="text-xs font-bold text-slate-900">{request.userName} · {request.grade}</p><p className="text-[11px] text-slate-500 mt-0.5">{request.message}</p></div>
                          </div>
                          <div className="flex gap-2">
                            <button type="button" onClick={() => reviewRequest(selectedGroup, request, true)} className="px-3 py-1.5 bg-emerald-600 text-white text-xs font-semibold rounded-lg"><Check className="w-3.5 h-3.5 inline mr-1" />Duyệt</button>
                            <button type="button" onClick={() => reviewRequest(selectedGroup, request, false)} className="px-3 py-1.5 bg-slate-100 text-slate-700 text-xs font-semibold rounded-lg"><X className="w-3.5 h-3.5 inline mr-1" />Từ chối</button>
                          </div>
                        </div>
                      ))}
                      {pendingRequests.length === 0 && <p className="py-4 text-xs text-slate-500">Không có yêu cầu nào đang chờ.</p>}
                    </div>
                  </div>
                )}
                <div className="bg-white rounded-2xl border border-slate-200 p-5">
                  <h2 className="text-sm font-bold text-slate-900">Danh sách thành viên</h2>
                  <div className="mt-3 divide-y divide-slate-100">
                    {selectedGroup.members.map((groupMember) => (
                      <div key={groupMember.id} className="py-3 flex items-center justify-between gap-3">
                        <div className="flex items-center gap-3"><img src={groupMember.avatar} alt="" className="w-10 h-10 rounded-xl object-cover" /><div><p className="text-xs font-bold text-slate-900">{groupMember.name}</p><p className="text-[11px] text-slate-500">{groupMember.grade}</p></div></div>
                        <span className={`text-[10px] font-semibold px-2.5 py-1 rounded-lg ${groupMember.role === 'Trưởng nhóm' ? 'bg-amber-100 text-amber-800' : 'bg-slate-100 text-slate-600'}`}>{groupMember.role}</span>
                      </div>
                    ))}
                  </div>
                </div>
              </section>
            )}
          </>
        )}

        <GroupFormModal isOpen={isGroupModalOpen} isEditing={Boolean(editingGroupId)} form={groupForm} setForm={setGroupForm} onClose={() => setIsGroupModalOpen(false)} onSubmit={saveGroup} />
        <JoinRequestModal isOpen={joinTargetId !== null} groupName={groups.find((group) => group.id === joinTargetId)?.name ?? ''} message={joinMessage} setMessage={setJoinMessage} onClose={() => setJoinTargetId(null)} onSubmit={submitJoinRequest} />
        <AnnouncementModal isOpen={isAnnouncementModalOpen} title={announcementTitle} content={announcementContent} setTitle={setAnnouncementTitle} setContent={setAnnouncementContent} onClose={() => setIsAnnouncementModalOpen(false)} onSubmit={postAnnouncement} />
        <ResourceModal isOpen={isResourceModalOpen} type={resourceType} resourceId={resourceId} setResourceId={setResourceId} notes={notes} flashcardSets={flashcardSets} onClose={() => setIsResourceModalOpen(false)} onSubmit={shareResource} />
        <ConfirmDialog isOpen={deleteTargetId !== null} onClose={() => setDeleteTargetId(null)} onConfirm={confirmDelete} title="Xóa nhóm học tập?" message="Toàn bộ bảng tin và danh sách yêu cầu của nhóm sẽ bị xóa khỏi thiết bị này." confirmText="Xóa nhóm" variant="danger" iconType="delete" />
      </div>
    );
  }

  return (
    <div id="study-groups-view" className="space-y-5 max-w-7xl mx-auto pb-10">
      <header className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <p className="text-[10px] font-bold tracking-[0.18em] text-indigo-600 uppercase">Cùng học, cùng tiến bộ</p>
          <h1 className="text-2xl font-bold text-slate-900 mt-1">Nhóm học tập</h1>
          <p className="text-xs text-slate-500 mt-1">Tìm bạn cùng mục tiêu, chia sẻ tài liệu và lên lịch học chung.</p>
        </div>
        <button type="button" onClick={openCreateModal} className="inline-flex items-center justify-center gap-1.5 px-4 py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded-xl shadow-2xs"><Plus className="w-4 h-4" /> Tạo nhóm mới</button>
      </header>

      <section className="bg-white p-3 sm:p-4 rounded-2xl border border-slate-200 space-y-3">
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-3">
          <div className="flex items-center gap-1 bg-slate-100 p-1 rounded-xl overflow-x-auto">
            <FilterButton active={groupFilter === 'my'} onClick={() => setGroupFilter('my')} label={`Nhóm của tôi (${groups.filter(isGroupMember).length})`} />
            <FilterButton active={groupFilter === 'discover'} onClick={() => setGroupFilter('discover')} label="Khám phá" />
            <FilterButton active={groupFilter === 'saved'} onClick={() => setGroupFilter('saved')} label={`Đã lưu (${groups.filter((group) => group.isSaved).length})`} />
          </div>
          <div className="relative lg:w-72">
            <Search className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
            <input value={searchQuery} onChange={(event) => setSearchQuery(event.target.value)} placeholder="Tìm tên, mục tiêu hoặc môn học..." className="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-indigo-400" />
          </div>
        </div>
        <div className="flex items-center gap-1.5 overflow-x-auto pb-1">
          <FilterButton active={subjectFilter === 'all'} onClick={() => setSubjectFilter('all')} label="Tất cả môn" />
          {subjectsList.map((subject) => <FilterButton key={subject} active={subjectFilter === subject} onClick={() => setSubjectFilter(subject)} label={subject} />)}
        </div>
      </section>

      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5">
        {filteredGroups.map((group) => {
          const color = subjectColorMap[group.subject] || subjectColorMap['Toán'];
          const pending = getPendingRequest(group, user.id);
          const full = group.memberCount >= memberCapacity(group);
          return (
            <article key={group.id} onClick={() => { setSelectedGroupId(group.id); setDetailTab('announcements'); }} className="bg-white rounded-2xl border border-slate-200 p-5 hover:border-indigo-300 hover:shadow-md transition-all cursor-pointer flex flex-col">
              <div className="flex items-center justify-between gap-2">
                <span className={`text-[10px] font-bold px-2 py-0.5 rounded-md ${color.bg} ${color.text} border ${color.border}`}>{group.subject}</span>
                <button type="button" onClick={(event) => toggleSaved(group, event)} className="p-1.5 text-slate-400 hover:text-rose-500" aria-label={group.isSaved ? 'Bỏ lưu nhóm' : 'Lưu nhóm'}><Heart className={`w-4 h-4 ${group.isSaved ? 'fill-rose-500 text-rose-500' : ''}`} /></button>
              </div>
              <h2 className="text-sm font-bold text-slate-900 mt-3 leading-snug">{group.name}</h2>
              <p className="text-xs text-slate-500 mt-1.5 line-clamp-2 leading-relaxed">{group.goal || group.description}</p>
              <div className="grid grid-cols-2 gap-2 mt-4 text-[11px] text-slate-600">
                <span className="flex items-center gap-1.5"><Users className="w-3.5 h-3.5" /> {group.memberCount}/{memberCapacity(group)}</span>
                <span className="flex items-center gap-1.5"><Video className="w-3.5 h-3.5" /> {group.studyMode ?? 'Chưa rõ'}</span>
                <span className="flex items-center gap-1.5 col-span-2"><Clock3 className="w-3.5 h-3.5" /> {group.meetingTime || 'Chưa đặt lịch'}</span>
              </div>
              <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between gap-2">
                <span className="text-[10px] text-slate-500 truncate">Trưởng nhóm: {group.ownerName}</span>
                {isGroupMember(group) ? (
                  <span className="text-[10px] font-semibold text-emerald-700 bg-emerald-50 px-2 py-1 rounded-lg">✓ Thành viên</span>
                ) : pending ? (
                  <span className="text-[10px] font-semibold text-amber-700 bg-amber-50 px-2 py-1 rounded-lg">Chờ duyệt</span>
                ) : (
                  <span className={`text-[10px] font-semibold px-2 py-1 rounded-lg ${full ? 'text-slate-500 bg-slate-100' : 'text-indigo-700 bg-indigo-50'}`}>{full ? 'Đã đủ người' : 'Còn chỗ'}</span>
                )}
              </div>
            </article>
          );
        })}
      </div>

      {filteredGroups.length === 0 && (
        <div className="bg-white rounded-2xl border border-dashed border-slate-300 p-12 text-center">
          <Users className="w-12 h-12 text-slate-300 mx-auto" />
          <h2 className="text-sm font-bold text-slate-800 mt-3">Không có nhóm phù hợp</h2>
          <p className="text-xs text-slate-500 mt-1">Thử đổi bộ lọc hoặc tạo nhóm học tập đầu tiên của bạn.</p>
          <button type="button" onClick={openCreateModal} className="mt-4 px-4 py-2 bg-indigo-600 text-white text-xs font-semibold rounded-xl">Tạo nhóm mới</button>
        </div>
      )}

      <GroupFormModal isOpen={isGroupModalOpen} isEditing={false} form={groupForm} setForm={setGroupForm} onClose={() => setIsGroupModalOpen(false)} onSubmit={saveGroup} />
      <JoinRequestModal isOpen={joinTargetId !== null} groupName={groups.find((group) => group.id === joinTargetId)?.name ?? ''} message={joinMessage} setMessage={setJoinMessage} onClose={() => setJoinTargetId(null)} onSubmit={submitJoinRequest} />
    </div>
  );
};

const InfoTile = ({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) => (
  <div className="bg-slate-50 rounded-xl p-3 min-w-0"><div className="flex items-center gap-1.5 text-[10px] text-slate-500">{icon}{label}</div><p className="text-xs font-bold text-slate-900 mt-1 truncate" title={value}>{value}</p></div>
);

const FilterButton = ({ active, onClick, label }: { active: boolean; onClick: () => void; label: string }) => (
  <button type="button" onClick={onClick} className={`px-3 py-1.5 rounded-lg text-xs font-semibold whitespace-nowrap transition-colors ${active ? 'bg-white text-indigo-600 shadow-2xs' : 'text-slate-600 hover:bg-white/70'}`}>{label}</button>
);

const TabButton = ({ active, onClick, icon, label }: { active: boolean; onClick: () => void; icon: React.ReactNode; label: string }) => (
  <button type="button" onClick={onClick} className={`px-4 py-2 text-xs font-bold rounded-xl whitespace-nowrap flex items-center gap-2 ${active ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}>{icon}{label}</button>
);

const SectionHeader = ({ title, subtitle, children }: { title: string; subtitle: string; children: React.ReactNode }) => (
  <div className="bg-white p-4 rounded-2xl border border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3"><div><h2 className="text-sm font-bold text-slate-900">{title}</h2><p className="text-xs text-slate-500 mt-0.5">{subtitle}</p></div>{children}</div>
);

const EmptyState = ({ text }: { text: string }) => (
  <div className="bg-white rounded-2xl border border-dashed border-slate-300 p-8 text-center text-xs text-slate-500"><BookOpen className="w-8 h-8 text-slate-300 mx-auto mb-2" />{text}</div>
);

const ResourceCard = ({ badge, title, description, action, onClick }: { badge: string; title: string; description: string; action: string; onClick: () => void }) => (
  <article className="bg-white rounded-2xl border border-slate-200 p-5"><span className="text-[10px] font-bold px-2 py-0.5 bg-indigo-50 text-indigo-700 rounded-md">{badge}</span><h3 className="text-xs font-bold text-slate-900 mt-2">{title}</h3><p className="text-[11px] text-slate-500 mt-1 line-clamp-2">{description}</p><button type="button" onClick={onClick} className="text-xs text-indigo-600 font-semibold mt-4">{action} →</button></article>
);

const formatDate = (value: string) => {
  const date = new Date(value.includes('T') ? value : value.replace(' ', 'T'));
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(date);
};

const formatChatTime = (value: string) => {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  const today = new Date();
  const sameDay = date.toDateString() === today.toDateString();
  return new Intl.DateTimeFormat('vi-VN', sameDay
    ? { hour: '2-digit', minute: '2-digit' }
    : { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }
  ).format(date);
};

interface GroupFormModalProps {
  isOpen: boolean;
  isEditing: boolean;
  form: GroupFormState;
  setForm: React.Dispatch<React.SetStateAction<GroupFormState>>;
  onClose: () => void;
  onSubmit: (event: React.FormEvent) => void;
}

const GroupFormModal = ({ isOpen, isEditing, form, setForm, onClose, onSubmit }: GroupFormModalProps) => (
  <Modal isOpen={isOpen} onClose={onClose} title={isEditing ? 'Chỉnh sửa nhóm học tập' : 'Tạo nhóm học tập mới'} subtitle="Điền đủ thông tin để tìm đúng bạn cùng học" maxWidth="2xl">
    <form onSubmit={onSubmit} className="space-y-4">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Field label="Tên nhóm" className="sm:col-span-2"><input required maxLength={100} value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} placeholder="Ví dụ: Ôn thi cuối kỳ Mobile Development" className="form-input" /></Field>
        <Field label="Môn học"><select value={form.subject} onChange={(event) => setForm((current) => ({ ...current, subject: event.target.value as Subject }))} className="form-input">{subjectsList.map((subject) => <option key={subject}>{subject}</option>)}</select></Field>
        <Field label="Số thành viên tối đa"><input type="number" min={2} max={100} value={form.capacity} onChange={(event) => setForm((current) => ({ ...current, capacity: Number(event.target.value) }))} className="form-input" /></Field>
        <Field label="Hình thức"><select value={form.studyMode} onChange={(event) => setForm((current) => ({ ...current, studyMode: event.target.value as StudyMode }))} className="form-input"><option>Online</option><option>Trực tiếp</option><option>Kết hợp</option></select></Field>
        <Field label="Thời gian học"><input maxLength={80} value={form.meetingTime} onChange={(event) => setForm((current) => ({ ...current, meetingTime: event.target.value }))} placeholder="Thứ 7, 19:00" className="form-input" /></Field>
        <Field label="Mô tả hoạt động" className="sm:col-span-2"><textarea required rows={3} maxLength={500} value={form.description} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} placeholder="Nhóm sẽ học gì và hoạt động như thế nào?" className="form-input" /></Field>
        <Field label="Mục tiêu học tập" className="sm:col-span-2"><textarea required rows={2} maxLength={300} value={form.goal} onChange={(event) => setForm((current) => ({ ...current, goal: event.target.value }))} placeholder="Ví dụ: Cùng đạt 8+ trong kỳ thi cuối kỳ" className="form-input" /></Field>
        <Field label="Liên kết hoặc email liên hệ" className="sm:col-span-2"><input value={form.contactLink} onChange={(event) => setForm((current) => ({ ...current, contactLink: event.target.value }))} placeholder="https://meet.google.com/... hoặc mailto:ban@example.com" className="form-input" /></Field>
      </div>
      <label className="flex items-center gap-2 text-xs text-slate-700"><input type="checkbox" checked={form.isPublic} onChange={(event) => setForm((current) => ({ ...current, isPublic: event.target.checked }))} className="w-4 h-4 accent-indigo-600" /> Cho phép sinh viên khác tìm thấy nhóm</label>
      <div className="flex justify-end gap-2 pt-3 border-t border-slate-100"><button type="button" onClick={onClose} className="px-4 py-2 text-xs font-semibold bg-slate-100 text-slate-700 rounded-xl">Hủy</button><button type="submit" className="px-4 py-2 text-xs font-semibold bg-indigo-600 text-white rounded-xl">{isEditing ? 'Lưu thay đổi' : 'Tạo nhóm'}</button></div>
    </form>
  </Modal>
);

const Field = ({ label, className = '', children }: { label: string; className?: string; children: React.ReactNode }) => <label className={`block text-xs font-bold text-slate-700 ${className}`}>{label}<div className="mt-1">{children}</div></label>;

const JoinRequestModal = ({ isOpen, groupName, message, setMessage, onClose, onSubmit }: { isOpen: boolean; groupName: string; message: string; setMessage: (value: string) => void; onClose: () => void; onSubmit: (event: React.FormEvent) => void }) => (
  <Modal isOpen={isOpen} onClose={onClose} title="Yêu cầu tham gia nhóm" subtitle={groupName} maxWidth="md"><form onSubmit={onSubmit} className="space-y-4"><Field label="Lời nhắn cho trưởng nhóm"><textarea rows={4} maxLength={300} value={message} onChange={(event) => setMessage(event.target.value)} placeholder="Giới thiệu mục tiêu và lý do bạn muốn tham gia..." className="form-input" /></Field><div className="flex justify-end gap-2"><button type="button" onClick={onClose} className="px-4 py-2 text-xs font-semibold bg-slate-100 rounded-xl">Hủy</button><button type="submit" className="px-4 py-2 text-xs font-semibold bg-indigo-600 text-white rounded-xl"><UserPlus className="w-3.5 h-3.5 inline mr-1" />Gửi yêu cầu</button></div></form></Modal>
);

const AnnouncementModal = ({ isOpen, title, content, setTitle, setContent, onClose, onSubmit }: { isOpen: boolean; title: string; content: string; setTitle: (value: string) => void; setContent: (value: string) => void; onClose: () => void; onSubmit: (event: React.FormEvent) => void }) => (
  <Modal isOpen={isOpen} onClose={onClose} title="Đăng thông báo mới" maxWidth="md"><form onSubmit={onSubmit} className="space-y-4"><Field label="Tiêu đề"><input required maxLength={120} value={title} onChange={(event) => setTitle(event.target.value)} className="form-input" /></Field><Field label="Nội dung"><textarea required rows={5} maxLength={1500} value={content} onChange={(event) => setContent(event.target.value)} className="form-input" /></Field><div className="flex justify-end gap-2"><button type="button" onClick={onClose} className="px-4 py-2 text-xs font-semibold bg-slate-100 rounded-xl">Hủy</button><button type="submit" className="px-4 py-2 text-xs font-semibold bg-indigo-600 text-white rounded-xl">Đăng thông báo</button></div></form></Modal>
);

const ResourceModal = ({ isOpen, type, resourceId, setResourceId, notes, flashcardSets, onClose, onSubmit }: { isOpen: boolean; type: ResourceType; resourceId: string; setResourceId: (value: string) => void; notes: Note[]; flashcardSets: FlashcardSet[]; onClose: () => void; onSubmit: (event: React.FormEvent) => void }) => {
  const resources = type === 'note' ? notes.map((item) => ({ id: item.id, label: `[${item.subject}] ${item.title}` })) : flashcardSets.map((item) => ({ id: item.id, label: `[${item.subject}] ${item.name}` }));
  return <Modal isOpen={isOpen} onClose={onClose} title={type === 'note' ? 'Chia sẻ ghi chú' : 'Chia sẻ bộ flashcard'} maxWidth="md"><form onSubmit={onSubmit} className="space-y-4"><Field label="Chọn tài liệu"><select required value={resourceId} onChange={(event) => setResourceId(event.target.value)} className="form-input">{resources.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</select></Field><div className="flex justify-end gap-2"><button type="button" onClick={onClose} className="px-4 py-2 text-xs font-semibold bg-slate-100 rounded-xl">Hủy</button><button type="submit" className="px-4 py-2 text-xs font-semibold bg-indigo-600 text-white rounded-xl">Chia sẻ</button></div></form></Modal>;
};
