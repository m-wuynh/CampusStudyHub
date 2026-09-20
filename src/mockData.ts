import { 
  UserProfile, 
  Note, 
  FlashcardSet, 
  CalendarEvent, 
  StudyGroup, 
  GradeEntry, 
  GradeRecord,
  AcademicGoal 
} from './types';

export const initialUser: UserProfile = {
  id: 'u-101',
  name: 'Nguyễn Minh Anh',
  email: 'minhanh.nguyen@thpt-hanoi.edu.vn',
  grade: 'Lớp 11A1 (Chuyên Tự Nhiên)',
  school: 'Trường THPT Chuyên Hà Nội - Amsterdam',
  avatarUrl: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150&auto=format&fit=crop&q=80',
  studyStreakDays: 5,
};

export const initialNotes: Note[] = [
  {
    id: 'note-1',
    title: "Ba định luật Newton & Phương pháp giải bài tập Động lực học",
    subject: 'Vật lý',
    content: `### 1. Định luật I Newton (Quán tính)
Nếu một vật không chịu tác dụng của lực nào hoặc chịu tác dụng của các lực có hợp lực bằng không, thì vật đang đứng yên sẽ tiếp tục đứng yên, đang chuyển động sẽ tiếp tục chuyển động thẳng đều.
- Quán tính là tính chất của mọi vật có xu hướng bảo toàn vận tốc cả về hướng và độ lớn.

### 2. Định luật II Newton (Động lực học)
Gia tốc của một vật cùng hướng với lực tác dụng lên vật. Độ lớn của gia tốc tỉ lệ thuận với độ lớn của lực tác dụng và tỉ lệ nghịch với khối lượng của vật:
$$\\vec{a} = \\frac{\\vec{F}}{m} \\iff \\vec{F} = m\\vec{a}$$

### 3. Định luật III Newton (Tương tác)
Trong mọi trường hợp, khi vật A tác dụng lên vật B một lực, thì vật B cũng tác dụng lại vật A một lực. Hai lực này là hai lực trực đối:
$$\\vec{F}_{BA} = -\\vec{F}_{AB}$$
- Đặc điểm: Cùng giá, cùng độ lớn, ngược chiều và đặt vào hai vật khác nhau (không cân bằng nhau).`,
    updatedAt: '2026-09-08T14:30:00',
    isPinned: true,
    tags: ['Lý 11', 'Cơ học', 'Newton', 'Thi giữa kỳ']
  },
  {
    id: 'note-2',
    title: 'Công thức Lượng giác & Phương trình bậc nhất đối với sin và cos',
    subject: 'Toán',
    content: `### 1. Công thức cộng lượng giác
- $\\sin(a \\pm b) = \\sin a \\cos b \\pm \\cos a \\sin b$
- $\\cos(a \\pm b) = \\cos a \\cos b \\mp \\sin a \\sin b$
- $\\tan(a \\pm b) = \\frac{\\tan a \\pm \\tan b}{1 \\mp \\tan a \\tan b}$

### 2. Công thức nhân đôi và hạ bậc
- $\\sin 2a = 2\\sin a \\cos a$
- $\\cos 2a = \\cos^2 a - \\sin^2 a = 2\\cos^2 a - 1 = 1 - 2\\sin^2 a$
- $\\sin^2 a = \\frac{1 - \\cos 2a}{2}; \\quad \\cos^2 a = \\frac{1 + \\cos 2a}{2}$

### 3. Phương trình $a\\sin x + b\\cos x = c$
- Điều kiện có nghiệm: $a^2 + b^2 \\ge c^2$.
- Chia cả 2 vế cho $\\sqrt{a^2 + b^2}$ để đưa về dạng $\\sin(x + \\alpha) = \\frac{c}{\\sqrt{a^2 + b^2}}$.`,
    updatedAt: '2026-09-07T09:15:00',
    isPinned: true,
    tags: ['Toán 11', 'Lượng giác', 'Công thức']
  },
  {
    id: 'note-3',
    title: 'Phân tích nhân vật Mị trong đêm tình mùa xuân (Vợ chồng A Phủ)',
    subject: 'Ngữ văn',
    content: `### 1. Bối cảnh tác động đến tâm hồn Mị
- Khung cảnh mùa xuân Hồng Ngài: Màu sắc sặc sỡ của váy hoa, tiếng sáo gọi bạn tình dặt dìu ven núi.
- Tiếng sáo là biểu tượng của tình yêu và tự do, đánh thức phần sự sống tưởng chừng đã chai sạn trong Mị.

### 2. Diễn biến tâm trạng và hành động
- Mị lén uống rượu: Uống ực từng bát như uống cả nỗi cay đắng, uất ức của bao năm câm lặng.
- Ý thức hồi sinh: "Mị thấy phơi phới trở lại", "Mị còn trẻ. Mị vẫn muốn đi chơi!".
- Bi kịch khi A Sử bắt trói: Mị bị trói đứng vào cột bằng thắt lưng nhưng tâm hồn vẫn đi theo tiếng sáo, lơ lửng giữa hiện thực tàn nhẫn và khát vọng tự do cháy bỏng.`,
    updatedAt: '2026-09-05T18:20:00',
    isPinned: false,
    tags: ['Ngữ văn 12', 'Văn học hiện thực', 'Tô Hoài']
  },
  {
    id: 'note-4',
    title: 'Tenses & Inversion Structures for THPTQG & IELTS',
    subject: 'Tiếng Anh',
    content: `### 1. Inversion with Negative Adverbials
- Hardly / Scarcely + had + S + V3/ed + when + S + V2/ed
- No sooner + had + S + V3/ed + than + S + V2/ed
- Not only + Auxiliary + S + V + but also ...
- Only when / Only after + S + V, + Auxiliary + S + V

### 2. Conditional Sentences Types & Inversion
- Type 1: Should + S + (not) + V(inf), S + will + V...
- Type 2: Were + S + to-V (hoặc Were + S + adj/noun), S + would + V...
- Type 3: Had + S + (not) + V3/ed, S + would have + V3/ed...`,
    updatedAt: '2026-09-04T11:00:00',
    isPinned: false,
    tags: ['Tiếng Anh', 'Ngữ pháp', 'Đảo ngữ']
  },
  {
    id: 'note-5',
    title: 'Phản ứng Oxi hóa - Khử & Phương pháp thăng bằng Electron',
    subject: 'Hóa học',
    content: `### Các bước cân bằng:
1. Xác định số oxi hóa của các nguyên tố thay đổi.
2. Viết quá trình oxi hóa (nhường e) và quá trình khử (nhận e).
3. Tìm hệ số thích hợp sao cho tổng e nhường = tổng e nhận.
4. Đặt hệ số vào phương trình và kiểm tra lại số nguyên tử hai vế (Kim loại -> Phi kim -> H -> O).`,
    updatedAt: '2026-09-02T16:45:00',
    isPinned: false,
    tags: ['Hóa 11', 'Oxi hóa khử']
  }
];

export const initialFlashcardSets: FlashcardSet[] = [
  {
    id: 'set-1',
    name: 'Công thức Vật lý 11 - Động lực học & Cơ năng',
    subject: 'Vật lý',
    description: 'Tổng hợp 20 công thức cốt lõi phần Động lực học chất điểm và các định luật bảo toàn cho kỳ thi giữa kỳ.',
    isPublic: true,
    authorName: 'Nguyễn Minh Anh',
    authorAvatar: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150&auto=format&fit=crop&q=80',
    createdAt: '2026-09-01',
    downloadsCount: 142,
    cards: [
      { id: 'c-1', question: 'Phát biểu công thức Định luật II Newton?', answer: 'F = m · a (Lực tác dụng bằng tích của khối lượng và gia tốc)', isLearned: true },
      { id: 'c-2', question: 'Công thức tính lực ma sát trượt?', answer: 'F_mst = μ_t · N (Trong đó μ_t là hệ số ma sát trượt, N là áp lực)', isLearned: true },
      { id: 'c-3', question: 'Công thức tính trọng lực tác dụng lên một vật?', answer: 'P = m · g (g ≈ 9.8 m/s² hoặc 10 m/s²)', isLearned: true },
      { id: 'c-4', question: 'Công thức tính công của lực F tác dụng khi dịch chuyển quãng đường s?', answer: 'A = F · s · cos(α) (α là góc hợp bởi hướng của lực và hướng dịch chuyển)', isLearned: false },
      { id: 'c-5', question: 'Công thức tính động năng của vật khối lượng m vận tốc v?', answer: 'W_đ = (1/2) · m · v²', isLearned: false },
      { id: 'c-6', question: 'Định luật bảo toàn cơ năng trong trọng trường?', answer: 'W = W_đ + W_t = const ⟺ (1/2)mv² + mgh = hằng số', isLearned: false },
      { id: 'c-7', question: 'Lực hướng tâm trong chuyển động tròn đều có độ lớn bằng bao nhiêu?', answer: 'F_ht = m · a_ht = m · (v² / r) = m · ω² · r', isLearned: true },
    ]
  },
  {
    id: 'set-2',
    name: 'Từ vựng Tiếng Anh Unit 1 - 3 THPT (Global Success)',
    subject: 'Tiếng Anh',
    description: 'Từ vựng chủ điểm Generation Gap, Healthy Living, và Green Cities kèm ví dụ câu.',
    isPublic: true,
    authorName: 'Trần Bảo Ngọc',
    authorAvatar: 'https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=150&auto=format&fit=crop&q=80',
    createdAt: '2026-09-03',
    downloadsCount: 230,
    cards: [
      { id: 'c-201', question: 'Generation gap (noun) nghĩa là gì?', answer: 'Khoảng cách giữa các thế hệ (sự khác biệt về tư duy, quan điểm sống giữa người trẻ và người già)', isLearned: true },
      { id: 'c-202', question: 'Breadwinner (noun) có nghĩa là gì?', answer: 'Người trụ cột kiếm tiền nuôi gia đình', isLearned: true },
      { id: 'c-203', question: 'Sustainable development nghĩa là gì?', answer: 'Phát triển bền vững (đáp ứng nhu cầu hiện tại mà không tổn hại tương lai)', isLearned: false },
      { id: 'c-204', question: 'Carbon footprint (noun) là gì?', answer: 'Dấu chân carbon (lượng khí thải nhà kính do cá nhân hoặc tổ chức tạo ra)', isLearned: false },
      { id: 'c-205', question: 'Nutritious (adjective) đồng nghĩa với từ nào?', answer: 'Nourishing, healthy, full of nutrients (giàu chất dinh dưỡng)', isLearned: true },
    ]
  },
  {
    id: 'set-3',
    name: 'Công thức Lượng giác 11 Siêu tốc',
    subject: 'Toán',
    description: 'Ghi nhớ nhanh công thức cộng, nhân đôi, biến đổi tích thành tổng và tổng thành tích.',
    isPublic: false,
    authorName: 'Nguyễn Minh Anh',
    authorAvatar: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150&auto=format&fit=crop&q=80',
    createdAt: '2026-09-04',
    downloadsCount: 56,
    cards: [
      { id: 'c-301', question: 'cos(a + b) = ?', answer: 'cos a · cos b - sin a · sin b', isLearned: true },
      { id: 'c-302', question: 'sin(a - b) = ?', answer: 'sin a · cos b - cos a · sin b', isLearned: true },
      { id: 'c-303', question: 'cos(2a) theo sin(a) bằng bao nhiêu?', answer: '1 - 2sin²(a)', isLearned: true },
      { id: 'c-304', question: 'tan(a + b) = ?', answer: '(tan a + tan b) / (1 - tan a · tan b)', isLearned: false },
      { id: 'c-305', question: 'Tập xác định của hàm số y = tan(x)?', answer: 'D = R \\ { π/2 + kπ, k ∈ Z }', isLearned: true }
    ]
  },
  {
    id: 'set-4',
    name: 'Màu sắc kết tủa & Tính chất khí Hóa vô cơ',
    subject: 'Hóa học',
    description: 'Bảng nhận biết ion, kết tủa Cu(OH)2, Fe(OH)3, BaSO4, AgCl... phục vụ giải bài toán vô cơ.',
    isPublic: true,
    authorName: 'Lê Hoàng Nam',
    authorAvatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150&auto=format&fit=crop&q=80',
    createdAt: '2026-09-05',
    downloadsCount: 188,
    cards: [
      { id: 'c-401', question: 'Fe(OH)2 kết tủa màu gì và để ngoài không khí chuyển sang màu gì?', answer: 'Kết tủa màu trắng xanh, để ngoài không khí hóa nâu đỏ thành Fe(OH)3', isLearned: true },
      { id: 'c-402', question: 'BaSO4 có tan trong axit mạnh không?', answer: 'Không tan trong nước và không tan trong các axit mạnh (HCl, HNO3)', isLearned: true },
      { id: 'c-403', question: 'Khí NO2 có màu gì và tính chất mùi?', answer: 'Khí màu nâu đỏ, mùi hắc, độc', isLearned: false },
    ]
  }
];

export const initialEvents: CalendarEvent[] = [
  {
    id: 'evt-1',
    title: 'Bài tập Đại số: Phương trình lượng giác',
    description: 'Hoàn thành bài tập SGK trang 28-30 và chuyên đề nâng cao.',
    date: '2026-09-09',
    startTime: '14:00',
    endTime: '15:30',
    category: 'homework',
    location: 'Bàn học ở nhà',
    completed: true
  },
  {
    id: 'evt-2',
    title: 'Ôn tập Động lực học & Định luật Newton',
    description: 'Xem lại Flashcard và giải 10 câu trắc nghiệm lý thuyết.',
    date: '2026-09-09',
    startTime: '16:00',
    endTime: '17:30',
    category: 'study',
    location: 'Thư viện trường',
    completed: true
  },
  {
    id: 'evt-3',
    title: 'Học nhóm Online: Luyện đề Tiếng Anh 11',
    description: 'Thảo luận đề Unit 1 & bài luận ngắn với nhóm THPT Amsterdam.',
    date: '2026-09-09',
    startTime: '19:30',
    endTime: '21:00',
    category: 'group',
    location: 'Google Meet / Phòng nhóm Study Hub',
    completed: true
  },
  {
    id: 'evt-4',
    title: 'Đọc trước tác phẩm Chí Phèo & soạn bài',
    description: 'Đọc văn bản phần tiếng chửi và tâm trạng sau khi gặp Thị Nở.',
    date: '2026-09-09',
    startTime: '21:30',
    endTime: '22:30',
    category: 'homework',
    location: 'Phòng học',
    completed: false
  },
  {
    id: 'evt-5',
    title: 'Kiểm tra 15 phút Vật lý',
    description: 'Phần chuyển động tròn đều và định luật I, II Newton.',
    date: '2026-09-11',
    startTime: '08:45',
    endTime: '09:00',
    category: 'exam',
    location: 'Phòng học 302 - Lớp 11A1',
    completed: false
  },
  {
    id: 'evt-6',
    title: 'Kiểm tra 1 tiết Toán Hình học không gian',
    description: 'Chủ đề quan hệ song song giữa đường thẳng và mặt phẳng.',
    date: '2026-09-15',
    startTime: '07:30',
    endTime: '08:15',
    category: 'exam',
    location: 'Phòng học 302',
    completed: false
  },
  {
    id: 'evt-7',
    title: 'Thực hành Thí nghiệm Hóa học',
    description: 'Nhận biết cation kim loại nhóm IA, IIA và nhôm.',
    date: '2026-09-12',
    startTime: '14:00',
    endTime: '16:00',
    category: 'study',
    location: 'Phòng Lab Hóa - Tầng 2',
    completed: false
  },
  {
    id: 'evt-8',
    title: 'Buổi sinh hoạt nhóm Toán Nâng cao',
    description: 'Giải các dạng bất đẳng thức Cauchy-Schwarz nâng cao.',
    date: '2026-09-13',
    startTime: '15:00',
    endTime: '17:00',
    category: 'group',
    location: 'Quán Cà phê Học tập The Bookworm',
    completed: false
  }
];

export const initialCalendarEvents = initialEvents;

export const initialStudyGroups: StudyGroup[] = [
  {
    id: 'grp-1',
    name: 'Toán học Chuyên sâu 11A1',
    subject: 'Toán',
    description: 'Nhóm cùng giải bài tập Toán nâng cao, thảo luận đề thi học sinh giỏi cấp trường và các mẹo giải nhanh casio.',
    ownerName: 'Nguyễn Minh Anh',
    ownerId: 'u-101',
    memberCount: 14,
    capacity: 20,
    goal: 'Ôn thi học sinh giỏi và đạt từ 9 điểm môn Toán.',
    studyMode: 'Kết hợp',
    meetingTime: 'Chủ nhật, 15:00',
    contactLink: 'https://meet.google.com/',
    isPublic: true,
    isSaved: true,
    createdAt: '2026-08-01T08:00:00',
    isMember: true,
    sharedNoteIds: ['note-2'],
    sharedSetIds: ['set-3'],
    members: [
      { id: 'm-1', name: 'Nguyễn Minh Anh', role: 'Trưởng nhóm', avatar: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150&auto=format&fit=crop&q=80', grade: 'Lớp 11A1' },
      { id: 'm-2', name: 'Trần Hoàng Nam', role: 'Phó nhóm', avatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150&auto=format&fit=crop&q=80', grade: 'Lớp 11A1' },
      { id: 'm-3', name: 'Đặng Thu Thảo', role: 'Thành viên', avatar: 'https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=150&auto=format&fit=crop&q=80', grade: 'Lớp 11A1' },
      { id: 'm-4', name: 'Lê Tuấn Kiệt', role: 'Thành viên', avatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=150&auto=format&fit=crop&q=80', grade: 'Lớp 11A2' },
      { id: 'm-5', name: 'Phạm Quỳnh Chi', role: 'Thành viên', avatar: 'https://images.unsplash.com/photo-1438761681033-6461ffad8d80?w=150&auto=format&fit=crop&q=80', grade: 'Lớp 11A1' },
    ],
    joinRequests: [
      {
        id: 'req-1',
        userId: 'u-205',
        userName: 'Phạm Gia Huy',
        userAvatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=150&auto=format&fit=crop&q=80',
        grade: 'Lớp 11A3',
        message: 'Mình muốn tham gia để cùng luyện đề học sinh giỏi Toán.',
        requestedAt: '2026-09-19T19:30:00',
        status: 'pending'
      }
    ],
    messages: [
      {
        id: 'msg-1',
        authorId: 'u-202',
        authorName: 'Trần Hoàng Nam',
        authorAvatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150&auto=format&fit=crop&q=80',
        content: 'Mọi người đã làm xong đề lượng giác số 1 chưa?',
        sentAt: '2026-09-20T18:30:00'
      },
      {
        id: 'msg-2',
        authorId: 'u-101',
        authorName: 'Nguyễn Minh Anh',
        authorAvatar: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150&auto=format&fit=crop&q=80',
        content: 'Mình đã làm xong phần 1. Tối nay mình gửi lời giải lên ghi chú chung nhé!',
        sentAt: '2026-09-20T18:34:00'
      },
      {
        id: 'msg-3',
        authorId: 'm-3',
        authorName: 'Đặng Thu Thảo',
        authorAvatar: 'https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=150&auto=format&fit=crop&q=80',
        content: 'Câu 4 mình chưa hiểu cách biến đổi. Mai mọi người giải thích giúp mình với nhé.',
        sentAt: '2026-09-20T18:38:00'
      }
    ],
    announcements: [
      {
        id: 'anc-1',
        author: 'Nguyễn Minh Anh',
        authorAvatar: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150&auto=format&fit=crop&q=80',
        date: '2026-09-08 20:00',
        title: 'Lịch ôn thi kiểm tra 1 tiết Đại số tuần tới',
        content: 'Chào các bạn! Chủ nhật tuần này (13/09) lúc 15:00 nhóm chúng mình sẽ có buổi gặp trực tiếp tại The Bookworm để luyện 3 đề mẫu lượng giác nhé. Mọi người nhớ làm trước đề số 1 đã gửi trong phần Tài liệu nhé!'
      },
      {
        id: 'anc-2',
        author: 'Trần Hoàng Nam',
        authorAvatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150&auto=format&fit=crop&q=80',
        date: '2026-09-06 14:15',
        title: 'Đã cập nhật bảng công thức lượng giác tóm tắt',
        content: 'Mình vừa bổ sung bộ Flashcard công thức lượng giác kèm các bài toán casio, các bạn vào mục Thẻ ghi nhớ của nhóm để học nhé.'
      }
    ]
  },
  {
    id: 'grp-2',
    name: 'Chiến Binh Vật Lý 11 - Ôn Thi Giữa Kỳ',
    subject: 'Vật lý',
    description: 'Chuyên đề cơ học chất điểm, định luật bảo toàn động lượng và cơ năng. Mục tiêu cả nhóm đạt 8.5+ môn Lý.',
    ownerName: 'Vũ Đức Minh',
    ownerId: 'u-202',
    memberCount: 22,
    capacity: 30,
    goal: 'Cùng đạt 8.5+ trong bài thi giữa kỳ Vật lý.',
    studyMode: 'Trực tiếp',
    meetingTime: 'Thứ Bảy, 14:00',
    contactLink: 'mailto:ducminh@example.edu.vn',
    isPublic: true,
    isSaved: false,
    createdAt: '2026-08-15T09:00:00',
    isMember: true,
    joinRequests: [],
    messages: [
      {
        id: 'msg-201',
        authorId: 'u-202',
        authorName: 'Vũ Đức Minh',
        authorAvatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=150&auto=format&fit=crop&q=80',
        content: 'Chiều thứ Bảy nhóm mình gặp ở thư viện như lịch nhé.',
        sentAt: '2026-09-19T20:10:00'
      }
    ],
    sharedNoteIds: ['note-1'],
    sharedSetIds: ['set-1'],
    members: [
      { id: 'm-10', name: 'Vũ Đức Minh', role: 'Trưởng nhóm', avatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=150&auto=format&fit=crop&q=80', grade: 'Lớp 11 Lý' },
      { id: 'm-1', name: 'Nguyễn Minh Anh', role: 'Thành viên', avatar: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150&auto=format&fit=crop&q=80', grade: 'Lớp 11A1' },
      { id: 'm-11', name: 'Hoàng Yến Nhi', role: 'Thành viên', avatar: 'https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=150&auto=format&fit=crop&q=80', grade: 'Lớp 11 Lý' },
    ],
    announcements: [
      {
        id: 'anc-201',
        author: 'Vũ Đức Minh',
        authorAvatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=150&auto=format&fit=crop&q=80',
        date: '2026-09-07 19:30',
        title: 'Nhắc nhở kiểm tra 15 phút thứ Sáu',
        content: 'Thầy vừa thông báo thứ Sáu sẽ kiểm tra 15 phút đột xuất về định luật Newton và công suất, các bạn chú ý ôn kỹ nhé!'
      }
    ]
  },
  {
    id: 'grp-3',
    name: 'CLB Tiếng Anh Giao Tiếp & Luyện Thi IELTS 7.5+',
    subject: 'Tiếng Anh',
    description: 'Luyện kỹ năng Nói - Viết hàng tuần theo chủ đề, học từ vựng học thuật C1 và trao đổi kinh nghiệm thi chứng chỉ.',
    ownerName: 'Trần Bảo Ngọc',
    ownerId: 'u-303',
    memberCount: 38,
    capacity: 40,
    goal: 'Luyện Speaking và Writing để hướng tới IELTS 7.5+.',
    studyMode: 'Online',
    meetingTime: 'Thứ Tư, 20:00',
    contactLink: 'https://meet.google.com/',
    isPublic: true,
    isSaved: true,
    createdAt: '2026-08-20T10:00:00',
    isMember: false,
    joinRequests: [],
    messages: [],
    sharedNoteIds: ['note-4'],
    sharedSetIds: ['set-2'],
    members: [
      { id: 'm-21', name: 'Trần Bảo Ngọc', role: 'Trưởng nhóm', avatar: 'https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=150&auto=format&fit=crop&q=80', grade: 'Lớp 11 Anh 1' },
    ],
    announcements: [
      {
        id: 'anc-301',
        author: 'Trần Bảo Ngọc',
        date: '2026-09-05 10:00',
        authorAvatar: 'https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=150&auto=format&fit=crop&q=80',
        title: 'Chủ đề Speaking tuần này: Artificial Intelligence in Education',
        content: 'Tối thứ 4 lúc 20:00 chúng mình sẽ có phòng Voice thảo luận các câu hỏi Part 2 & Part 3 về công nghệ AI trong trường học.'
      }
    ]
  },
  {
    id: 'grp-4',
    name: 'Đội tuyển Hóa Học Thực Hành 11',
    subject: 'Hóa học',
    description: 'Chia sẻ kinh nghiệm làm bài thi chọn học sinh giỏi, phương pháp giải nhanh bài toán hỗn hợp kim loại và HNO3.',
    ownerName: 'Lê Hoàng Nam',
    ownerId: 'u-404',
    memberCount: 16,
    capacity: 20,
    goal: 'Ôn thi chọn học sinh giỏi và rèn kỹ năng thực hành Hóa học.',
    studyMode: 'Trực tiếp',
    meetingTime: 'Thứ Sáu, 16:30',
    contactLink: 'mailto:hoangnam@example.edu.vn',
    isPublic: true,
    isSaved: false,
    createdAt: '2026-08-25T15:00:00',
    isMember: false,
    joinRequests: [],
    messages: [],
    sharedNoteIds: ['note-5'],
    sharedSetIds: ['set-4'],
    members: [
      { id: 'm-31', name: 'Lê Hoàng Nam', role: 'Trưởng nhóm', avatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150&auto=format&fit=crop&q=80', grade: 'Lớp 11 Hóa' }
    ],
    announcements: []
  }
];

export const initialGrades: GradeEntry[] = [
  // Toán (TB: 8.8)
  { id: 'g-1', subject: 'Toán', examType: 'Miệng / 15 phút', score: 9.0, coefficient: 1, semester: 'Học kỳ 1', date: '2026-09-02' },
  { id: 'g-2', subject: 'Toán', examType: 'Miệng / 15 phút', score: 8.5, coefficient: 1, semester: 'Học kỳ 1', date: '2026-09-05' },
  { id: 'g-3', subject: 'Toán', examType: '1 tiết', score: 9.0, coefficient: 2, semester: 'Học kỳ 1', date: '2026-09-07' },

  // Vật lý (TB: 7.8)
  { id: 'g-4', subject: 'Vật lý', examType: 'Miệng / 15 phút', score: 7.5, coefficient: 1, semester: 'Học kỳ 1', date: '2026-09-03' },
  { id: 'g-5', subject: 'Vật lý', examType: '1 tiết', score: 8.0, coefficient: 2, semester: 'Học kỳ 1', date: '2026-09-06' },

  // Hóa học (TB: 8.2)
  { id: 'g-6', subject: 'Hóa học', examType: 'Miệng / 15 phút', score: 8.0, coefficient: 1, semester: 'Học kỳ 1', date: '2026-09-04' },
  { id: 'g-7', subject: 'Hóa học', examType: '1 tiết', score: 8.3, coefficient: 2, semester: 'Học kỳ 1', date: '2026-09-08' },

  // Ngữ văn (TB: 8.0)
  { id: 'g-8', subject: 'Ngữ văn', examType: 'Miệng / 15 phút', score: 8.0, coefficient: 1, semester: 'Học kỳ 1', date: '2026-09-03' },
  { id: 'g-9', subject: 'Ngữ văn', examType: '1 tiết', score: 8.0, coefficient: 2, semester: 'Học kỳ 1', date: '2026-09-07' },

  // Tiếng Anh (TB: 8.9)
  { id: 'g-10', subject: 'Tiếng Anh', examType: 'Miệng / 15 phút', score: 9.5, coefficient: 1, semester: 'Học kỳ 1', date: '2026-09-01' },
  { id: 'g-11', subject: 'Tiếng Anh', examType: '1 tiết', score: 8.6, coefficient: 2, semester: 'Học kỳ 1', date: '2026-09-05' },

  // Sinh học (TB: 8.5)
  { id: 'g-12', subject: 'Sinh học', examType: 'Miệng / 15 phút', score: 8.5, coefficient: 1, semester: 'Học kỳ 1', date: '2026-09-04' },
];

export const initialGradeRecords: GradeRecord[] = [
  {
    id: 'gr-1',
    subject: 'Toán',
    semester: 'Học kỳ 1',
    oralScores: [9.0],
    test15mScores: [8.5, 9.0],
    test1PeriodScores: [9.0],
    midtermScore: 9.0,
    finalScore: 8.8,
    averageScore: 8.9,
  },
  {
    id: 'gr-2',
    subject: 'Vật lý',
    semester: 'Học kỳ 1',
    oralScores: [7.5],
    test15mScores: [8.0],
    test1PeriodScores: [8.0],
    midtermScore: 8.2,
    finalScore: 8.5,
    averageScore: 8.1,
  },
  {
    id: 'gr-3',
    subject: 'Hóa học',
    semester: 'Học kỳ 1',
    oralScores: [8.0],
    test15mScores: [8.5],
    test1PeriodScores: [8.3],
    midtermScore: 8.5,
    finalScore: 8.7,
    averageScore: 8.5,
  },
  {
    id: 'gr-4',
    subject: 'Ngữ văn',
    semester: 'Học kỳ 1',
    oralScores: [8.0],
    test15mScores: [8.0],
    test1PeriodScores: [8.2],
    midtermScore: 8.0,
    finalScore: 8.5,
    averageScore: 8.2,
  },
  {
    id: 'gr-5',
    subject: 'Tiếng Anh',
    semester: 'Học kỳ 1',
    oralScores: [9.5],
    test15mScores: [9.0, 9.5],
    test1PeriodScores: [8.8],
    midtermScore: 9.2,
    finalScore: 9.0,
    averageScore: 9.1,
  },
  {
    id: 'gr-6',
    subject: 'Sinh học',
    semester: 'Học kỳ 1',
    oralScores: [8.5],
    test15mScores: [8.5],
    test1PeriodScores: [8.5],
    midtermScore: 8.7,
    finalScore: 8.5,
    averageScore: 8.6,
  },
  {
    id: 'gr-7',
    subject: 'Lịch sử',
    semester: 'Học kỳ 1',
    oralScores: [8.5],
    test15mScores: [8.0],
    test1PeriodScores: [8.0],
    midtermScore: 8.5,
    finalScore: 8.2,
    averageScore: 8.3,
  },
  {
    id: 'gr-8',
    subject: 'Địa lý',
    semester: 'Học kỳ 1',
    oralScores: [8.0],
    test15mScores: [8.5],
    test1PeriodScores: [8.5],
    midtermScore: 8.2,
    finalScore: 8.5,
    averageScore: 8.4,
  },
  {
    id: 'gr-9',
    subject: 'Tin học',
    semester: 'Học kỳ 1',
    oralScores: [9.5],
    test15mScores: [9.5],
    test1PeriodScores: [9.0],
    midtermScore: 9.5,
    finalScore: 9.5,
    averageScore: 9.4,
  }
];

export const initialGoals: AcademicGoal[] = [
  {
    id: 'goal-1',
    title: 'Đạt điểm 9.0 Giữa kỳ môn Toán',
    subject: 'Toán',
    currentScore: 8.8,
    targetScore: 9.0,
    targetDate: '2026-10-20',
    deadline: '2026-10-20',
    progress: 75,
    status: 'In progress',
    isCompleted: false
  },
  {
    id: 'goal-2',
    title: 'Hoàn thành toàn bộ bài tập Hóa học trước thứ Sáu',
    subject: 'Hóa học',
    currentScore: 8.0,
    targetScore: 8.5,
    targetDate: '2026-09-11',
    deadline: '2026-09-11',
    progress: 60,
    status: 'In progress',
    isCompleted: false
  },
  {
    id: 'goal-3',
    title: 'Học 100 từ vựng Tiếng Anh mới trong tuần',
    subject: 'Tiếng Anh',
    currentScore: 8.9,
    targetScore: 9.5,
    targetDate: '2026-09-14',
    deadline: '2026-09-14',
    progress: 45,
    status: 'In progress',
    isCompleted: false
  },
  {
    id: 'goal-4',
    title: 'Đạt điểm tối đa 10 kiểm tra 15 phút Vật lý',
    subject: 'Vật lý',
    currentScore: 7.8,
    targetScore: 10.0,
    targetDate: '2026-09-18',
    deadline: '2026-09-18',
    progress: 100,
    status: 'Completed',
    isCompleted: true
  }
];

export const subjectColorMap: Record<string, { bg: string; text: string; border: string; accent: string }> = {
  'Toán': { bg: 'bg-blue-50', text: 'text-blue-700', border: 'border-blue-200', accent: '#2563eb' },
  'Vật lý': { bg: 'bg-indigo-50', text: 'text-indigo-700', border: 'border-indigo-200', accent: '#4f46e5' },
  'Hóa học': { bg: 'bg-amber-50', text: 'text-amber-800', border: 'border-amber-200', accent: '#d97706' },
  'Ngữ văn': { bg: 'bg-rose-50', text: 'text-rose-700', border: 'border-rose-200', accent: '#e11d48' },
  'Tiếng Anh': { bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200', accent: '#059669' },
  'Sinh học': { bg: 'bg-teal-50', text: 'text-teal-700', border: 'border-teal-200', accent: '#0d9488' },
  'Lịch sử': { bg: 'bg-orange-50', text: 'text-orange-800', border: 'border-orange-200', accent: '#ea580c' },
  'Địa lý': { bg: 'bg-cyan-50', text: 'text-cyan-800', border: 'border-cyan-200', accent: '#0891b2' },
  'Tin học': { bg: 'bg-violet-50', text: 'text-violet-700', border: 'border-violet-200', accent: '#7c3aed' },
};
