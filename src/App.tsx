import React, { useState } from 'react';
import { 
  UserProfile, 
  NavigationPage, 
  Note, 
  FlashcardSet, 
  CalendarEvent, 
  StudyGroup, 
  GradeRecord, 
  AcademicGoal 
} from './types';
import { 
  initialUser, 
  initialNotes, 
  initialFlashcardSets, 
  initialCalendarEvents, 
  initialStudyGroups, 
  initialGradeRecords, 
  initialGoals 
} from './mockData';
import { Sidebar } from './components/Sidebar';
import { TopBar } from './components/TopBar';
import { Toast, ToastMessage } from './components/Toast';
import { ConfirmDialog } from './components/ConfirmDialog';

// Views
import { LoginView } from './views/LoginView';
import { DashboardView } from './views/DashboardView';
import { NotesView } from './views/NotesView';
import { FlashcardsView } from './views/FlashcardsView';
import { CalendarView } from './views/CalendarView';
import { StudyGroupsView } from './views/StudyGroupsView';
import { GradesGoalsView } from './views/GradesGoalsView';
import { SettingsView } from './views/SettingsView';

export function App() {
  // Authentication State
  const [isAuthenticated, setIsAuthenticated] = useState(true);

  // User Profile
  const [user, setUser] = useState<UserProfile>(initialUser);

  // Navigation State
  const [currentPage, setCurrentPage] = useState<NavigationPage>('dashboard');

  // App Data Collections
  const [notes, setNotes] = useState<Note[]>(initialNotes);
  const [flashcardSets, setFlashcardSets] = useState<FlashcardSet[]>(initialFlashcardSets);
  const [events, setEvents] = useState<CalendarEvent[]>(initialCalendarEvents);
  const [groups, setGroups] = useState<StudyGroup[]>(initialStudyGroups);
  const [grades, setGrades] = useState<GradeRecord[]>(initialGradeRecords);
  const [goals, setGoals] = useState<AcademicGoal[]>(initialGoals);

  // Deep linking between views
  const [activeNoteId, setActiveNoteId] = useState<string | null>(null);
  const [activeStudyingSetId, setActiveStudyingSetId] = useState<string | null>(null);
  const [isCalendarCreateModalOpen, setIsCalendarCreateModalOpen] = useState(false);

  // Toast System
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  // Logout Dialog
  const [isLogoutConfirmOpen, setIsLogoutConfirmOpen] = useState(false);

  const showToast = (message: string, type: 'success' | 'error' | 'info' = 'info') => {
    const id = `toast-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;
    setToasts((prev) => [...prev, { id, message, type }]);
  };

  const removeToast = (id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  // --- Handlers for Notes ---
  const handleSaveNote = (savedNote: Note) => {
    setNotes((prev) => {
      const exists = prev.some((n) => n.id === savedNote.id);
      if (exists) {
        return prev.map((n) => (n.id === savedNote.id ? savedNote : n));
      }
      return [savedNote, ...prev];
    });
  };

  const handleDeleteNote = (noteId: string) => {
    setNotes((prev) => prev.filter((n) => n.id !== noteId));
    if (activeNoteId === noteId) {
      setActiveNoteId(null);
    }
  };

  const handleOpenNoteFromOtherView = (noteId: string) => {
    setActiveNoteId(noteId);
    setCurrentPage('notes');
  };

  // --- Handlers for Flashcards ---
  const handleSaveFlashcardSet = (savedSet: FlashcardSet) => {
    setFlashcardSets((prev) => {
      const exists = prev.some((s) => s.id === savedSet.id);
      if (exists) {
        return prev.map((s) => (s.id === savedSet.id ? savedSet : s));
      }
      return [savedSet, ...prev];
    });
  };

  const handleDeleteFlashcardSet = (setId: string) => {
    setFlashcardSets((prev) => prev.filter((s) => s.id !== setId));
    if (activeStudyingSetId === setId) {
      setActiveStudyingSetId(null);
    }
  };

  const handleOpenFlashcardFromOtherView = (setId: string) => {
    setActiveStudyingSetId(setId);
    setCurrentPage('flashcards');
  };

  // --- Handlers for Calendar ---
  const handleSaveEvent = (savedEvent: CalendarEvent) => {
    setEvents((prev) => {
      const exists = prev.some((e) => e.id === savedEvent.id);
      if (exists) {
        return prev.map((e) => (e.id === savedEvent.id ? savedEvent : e));
      }
      return [...prev, savedEvent];
    });
  };

  const handleDeleteEvent = (eventId: string) => {
    setEvents((prev) => prev.filter((e) => e.id !== eventId));
  };

  const handleToggleEventComplete = (eventId: string) => {
    setEvents((prev) =>
      prev.map((e) => (e.id === eventId ? { ...e, completed: !e.completed } : e))
    );
  };

  // --- Handlers for Groups ---
  const handleSaveGroup = (savedGroup: StudyGroup) => {
    setGroups((prev) => {
      const exists = prev.some((g) => g.id === savedGroup.id);
      if (exists) {
        return prev.map((g) => (g.id === savedGroup.id ? savedGroup : g));
      }
      return [savedGroup, ...prev];
    });
  };

  // --- Handlers for Grades & Goals ---
  const handleSaveGrade = (savedGrade: GradeRecord) => {
    setGrades((prev) => {
      const exists = prev.some((g) => g.id === savedGrade.id);
      if (exists) {
        return prev.map((g) => (g.id === savedGrade.id ? savedGrade : g));
      }
      return [...prev, savedGrade];
    });
  };

  const handleDeleteGrade = (gradeId: string) => {
    setGrades((prev) => prev.filter((g) => g.id !== gradeId));
  };

  const handleSaveGoal = (savedGoal: AcademicGoal) => {
    setGoals((prev) => {
      const exists = prev.some((g) => g.id === savedGoal.id);
      if (exists) {
        return prev.map((g) => (g.id === savedGoal.id ? savedGoal : g));
      }
      return [savedGoal, ...prev];
    });
  };

  const handleDeleteGoal = (goalId: string) => {
    setGoals((prev) => prev.filter((g) => g.id !== goalId));
  };

  const handleToggleGoalComplete = (goalId: string) => {
    setGoals((prev) =>
      prev.map((g) => {
        if (g.id === goalId) {
          const nextStatus = g.status === 'Completed' ? 'In progress' : 'Completed';
          return {
            ...g,
            status: nextStatus,
            progress: nextStatus === 'Completed' ? 100 : 50,
          };
        }
        return g;
      })
    );
  };

  // --- Quick Action Handlers ---
  const handleQuickAction = (action: 'new-note' | 'new-flashcard' | 'new-event' | 'new-group') => {
    switch (action) {
      case 'new-note':
        setActiveNoteId(null);
        setCurrentPage('notes');
        showToast('Đang chuyển đến soạn ghi chú mới.', 'info');
        break;
      case 'new-flashcard':
        setActiveStudyingSetId(null);
        setCurrentPage('flashcards');
        showToast('Đang chuyển đến danh mục Flashcards.', 'info');
        break;
      case 'new-event':
        setCurrentPage('calendar');
        setIsCalendarCreateModalOpen(true);
        break;
      case 'new-group':
        setCurrentPage('groups');
        showToast('Đang chuyển đến danh mục Nhóm học tập.', 'info');
        break;
    }
  };

  // --- Authentication Handlers ---
  const handleLoginSuccess = (loggedInUser: UserProfile) => {
    setUser(loggedInUser);
    setIsAuthenticated(true);
    setCurrentPage('dashboard');
    showToast(`Chào mừng ${loggedInUser.name} đã quay trở lại Study Hub!`, 'success');
  };

  const handleLogout = () => {
    setIsAuthenticated(false);
    setIsLogoutConfirmOpen(false);
    showToast('Bạn đã đăng xuất tài khoản an toàn.', 'info');
  };

  // If not authenticated, render the dedicated Login / Register View
  if (!isAuthenticated) {
    return (
      <div className="min-h-screen bg-slate-50 text-slate-900 flex flex-col justify-between font-sans">
        <LoginView onLogin={handleLoginSuccess} showToast={showToast} />
        <Toast toasts={toasts} onCloseToast={removeToast} />
      </div>
    );
  }

  // Authenticated Application Shell
  return (
    <div id="study-hub-app" className="min-h-screen bg-slate-50 text-slate-900 flex flex-col font-sans">
      {/* Responsive Left Navigation Sidebar */}
      <Sidebar
        currentPage={currentPage}
        user={user}
        onNavigate={(page) => {
          setCurrentPage(page);
          window.scrollTo({ top: 0, behavior: 'smooth' });
        }}
        onLogout={() => setIsLogoutConfirmOpen(true)}
      />

      {/* Main Content Area (Offset for desktop sidebar w-64) */}
      <div className="lg:pl-64 flex flex-col min-h-screen">
        {/* Top Header Bar */}
        <TopBar
          user={user}
          currentPage={currentPage}
          onNavigate={(page) => setCurrentPage(page)}
          onQuickAction={handleQuickAction}
        />

        {/* Dynamic Page Views */}
        <main className="flex-1 p-4 sm:p-6 lg:p-8">
          {currentPage === 'dashboard' && (
            <DashboardView
              user={user}
              notes={notes}
              flashcardSets={flashcardSets}
              events={events}
              groups={groups}
              grades={grades}
              goals={goals}
              onNavigate={(page) => setCurrentPage(page)}
              onToggleEventComplete={handleToggleEventComplete}
              onQuickAction={handleQuickAction}
              onOpenStudySet={(setId) => handleOpenFlashcardFromOtherView(setId)}
              onOpenNote={handleOpenNoteFromOtherView}
            />
          )}

          {currentPage === 'notes' && (
            <NotesView
              notes={notes}
              onSaveNote={handleSaveNote}
              onDeleteNote={handleDeleteNote}
              showToast={showToast}
              initialSelectedNoteId={activeNoteId}
              onClearInitialSelectedNoteId={() => setActiveNoteId(null)}
            />
          )}

          {currentPage === 'flashcards' && (
            <FlashcardsView
              user={user}
              flashcardSets={flashcardSets}
              onSaveSet={handleSaveFlashcardSet}
              onDeleteSet={handleDeleteFlashcardSet}
              showToast={showToast}
              initialStudyingSetId={activeStudyingSetId}
              onClearStudyingSetId={() => setActiveStudyingSetId(null)}
            />
          )}

          {currentPage === 'calendar' && (
            <CalendarView
              user={user}
              events={events}
              onSaveEvent={handleSaveEvent}
              onDeleteEvent={handleDeleteEvent}
              onToggleEventComplete={handleToggleEventComplete}
              showToast={showToast}
              isCreateModalOpenExternal={isCalendarCreateModalOpen}
              onCloseCreateModalExternal={() => setIsCalendarCreateModalOpen(false)}
            />
          )}

          {currentPage === 'groups' && (
            <StudyGroupsView
              user={user}
              groups={groups}
              notes={notes}
              flashcardSets={flashcardSets}
              onSaveGroup={handleSaveGroup}
              showToast={showToast}
              onOpenNote={handleOpenNoteFromOtherView}
              onOpenStudySet={handleOpenFlashcardFromOtherView}
            />
          )}

          {currentPage === 'grades' && (
            <GradesGoalsView
              user={user}
              grades={grades}
              goals={goals}
              onSaveGrade={handleSaveGrade}
              onDeleteGrade={handleDeleteGrade}
              onSaveGoal={handleSaveGoal}
              onDeleteGoal={handleDeleteGoal}
              onToggleGoalComplete={handleToggleGoalComplete}
              showToast={showToast}
            />
          )}

          {currentPage === 'settings' && (
            <SettingsView
              user={user}
              onUpdateUser={(updated) => setUser(updated)}
              showToast={showToast}
            />
          )}
        </main>
      </div>

      {/* Global Toast Notification System */}
      <Toast toasts={toasts} onCloseToast={removeToast} />

      {/* Logout Confirmation Dialog */}
      <ConfirmDialog
        isOpen={isLogoutConfirmOpen}
        onClose={() => setIsLogoutConfirmOpen(false)}
        onConfirm={handleLogout}
        title="Đăng xuất khỏi Study Hub?"
        message="Bạn có chắc chắn muốn đăng xuất khỏi phiên học tập hiện tại? Dữ liệu ghi nhớ của bạn đã được lưu."
        confirmText="Đăng xuất"
        cancelText="Ở lại"
        variant="warning"
        iconType="logout"
      />
    </div>
  );
}

export default App;
