export type Subject = 
  | 'Toán' 
  | 'Vật lý' 
  | 'Hóa học' 
  | 'Ngữ văn' 
  | 'Tiếng Anh' 
  | 'Sinh học' 
  | 'Lịch sử' 
  | 'Địa lý' 
  | 'Tin học'
  | 'GDCD';

export interface UserProfile {
  id: string;
  name: string;
  email: string;
  grade: string;
  school: string;
  avatarUrl: string;
  studyStreakDays: number;
  targetGpa?: number;
}

export interface Note {
  id: string;
  title: string;
  subject: Subject;
  content: string;
  updatedAt: string;
  isPinned?: boolean;
  tags?: string[];
}

export interface FlashcardItem {
  id: string;
  question: string;
  answer: string;
  isLearned?: boolean;
}

export interface FlashcardSet {
  id: string;
  name: string;
  subject: Subject;
  description: string;
  cards: FlashcardItem[];
  isPublic: boolean;
  authorName: string;
  authorAvatar?: string;
  createdAt: string;
  downloadsCount?: number;
}

export type EventCategory = 'study' | 'homework' | 'exam' | 'group';

export interface CalendarEvent {
  id: string;
  title: string;
  description?: string;
  date: string; // YYYY-MM-DD
  startTime: string; // HH:mm
  endTime: string; // HH:mm
  category: EventCategory;
  location?: string;
  completed?: boolean;
}

export interface GroupAnnouncement {
  id: string;
  author: string;
  authorAvatar: string;
  date: string;
  title: string;
  content: string;
}

export interface GroupMember {
  id: string;
  name: string;
  role: 'Trưởng nhóm' | 'Thành viên' | 'Phó nhóm';
  avatar: string;
  grade: string;
}

export interface StudyGroup {
  id: string;
  name: string;
  subject: Subject;
  description: string;
  ownerName: string;
  memberCount: number;
  members: GroupMember[];
  announcements: GroupAnnouncement[];
  sharedNoteIds: string[];
  sharedSetIds: string[];
  isMember?: boolean;
}

export type ExamType = 'Miệng / 15 phút' | '1 tiết' | 'Giữa kỳ' | 'Cuối kỳ';

export interface GradeEntry {
  id: string;
  subject: Subject;
  examType: ExamType;
  score: number; // 0 - 10
  coefficient: number; // 1, 2, or 3
  semester: 'Học kỳ 1' | 'Học kỳ 2';
  date: string;
}

export interface GradeRecord {
  id: string;
  subject: Subject;
  semester: 'Học kỳ 1' | 'Học kỳ 2';
  oralScores: number[]; // Điểm kiểm tra miệng
  test15mScores: number[]; // Điểm 15 phút
  test1PeriodScores: number[]; // Điểm 1 tiết
  midtermScore?: number; // Điểm giữa kỳ (hệ số 2)
  finalScore?: number; // Điểm cuối kỳ (hệ số 3)
  averageScore: number; // Điểm trung bình môn
}

export interface AcademicGoal {
  id: string;
  title: string;
  subject: Subject;
  currentScore?: number;
  targetScore?: number;
  deadline?: string;
  targetDate: string;
  progress: number;
  status: 'In progress' | 'Completed';
  isCompleted?: boolean;
}

export type NavigationPage = 
  | 'dashboard' 
  | 'notes' 
  | 'flashcards' 
  | 'calendar' 
  | 'groups' 
  | 'grades' 
  | 'settings';
