import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { InternTask, User } from '../../models';
import { InternService, UserService } from '../../services/api.services';
import { ToastService } from '../../services/toast.service';
import { NavbarComponent } from '../navbar/navbar.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

@Component({
  selector: 'app-mentor-dashboard',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    DatePipe,
    NavbarComponent,
    SidebarComponent
  ],
  templateUrl: './mentor-dashboard.component.html',
  styleUrl: './mentor-dashboard.component.css'
})
export class MentorDashboardComponent implements OnInit {

  private intSvc = inject(InternService);
  private usrSvc = inject(UserService);
  private toast  = inject(ToastService);
  private fb     = inject(FormBuilder);

  // ========================
  // STATE
  // ========================
  interns        = signal<User[]>([]);
  selectedIntern = signal<User | null>(null);
  tasks          = signal<InternTask[]>([]);

  // ========================
  // SEARCH
  // ========================
  search = signal('');

  filteredInterns = computed(() => {
    const list = this.interns() ?? [];
    const q = this.search().toLowerCase();

    if (!q) return list;

    return list.filter(i =>
      (i.name ?? '').toLowerCase().includes(q) ||
      (i.email ?? '').toLowerCase().includes(q)
    );
  });

  // ========================
  // MODAL FORM
  // ========================
  showModal = signal(false);

  taskForm = this.fb.group({
    taskTitle: ['', Validators.required],
    description: [''],
    dueDate: ['']
  });

  // ========================
  // INIT
  // ========================
  ngOnInit() {
    this.loadInterns();
  }

  // ========================
  // LOAD INTERNS
  // ========================
  private loadInterns(): void {
    this.usrSvc.getAll().subscribe({
      next: (res: any) => {
        let data: any[] = [];

        if (Array.isArray(res)) data = res;
        else if (Array.isArray(res?.data)) data = res.data;
        else if (Array.isArray(res?.data?.data)) data = res.data.data;

        const interns = data.filter(u =>
          (u.role || '').toLowerCase() === 'intern'
        );

        this.interns.set(interns);
      },
      error: () => {
        this.toast.error('Error', 'Failed to load interns');
        this.interns.set([]);
      }
    });
  }

  // ========================
  // SELECT INTERN
  // ========================
  selectIntern(intern: User) {
    this.selectedIntern.set(intern);
    this.loadTasks(intern.id);
  }

  // ========================
  // LOAD TASKS
  // ========================
  loadTasks(internId: number) {
    this.intSvc.getTasks(internId).subscribe({
      next: (res: any) => {
        let data: any[] = [];

        if (Array.isArray(res)) data = res;
        else if (Array.isArray(res?.data)) data = res.data;
        else if (Array.isArray(res?.data?.data)) data = res.data.data;

        this.tasks.set(data);
      },
      error: () => {
        this.toast.error('Failed to load tasks', '');
        this.tasks.set([]);
      }
    });
  }

  // ========================
  // ASSIGN TASK
  // ========================
  openAssign() {
    if (!this.selectedIntern()) {
      this.toast.warning('Select an intern first', '');
      return;
    }
    this.taskForm.reset();
    this.showModal.set(true);
  }

  assignTask() {
    if (this.taskForm.invalid) {
      this.taskForm.markAllAsTouched();
      return;
    }

    const intern = this.selectedIntern();
    if (!intern) return;

    const v = this.taskForm.value;

    const req = {
      internId: intern.id,
      taskTitle: v.taskTitle!,
      description: v.description || '',
      dueDate: v.dueDate || undefined
    };

    this.intSvc.createTask(req).subscribe({
      next: () => {
        this.toast.success('Task Assigned', '');
        this.showModal.set(false);
        this.loadTasks(intern.id);
      },
      error: () => this.toast.error('Failed', '')
    });
  }

  // ========================
  // ✅ UPDATE STATUS (FIXED)
  // ========================
  markCompleted(task: InternTask) {

    const req = {
      taskTitle: task.taskTitle,
      description: task.description,
      dueDate: task.dueDate,
      status: 2   // ✅ Completed = 2
    };

    this.intSvc.updateTask(task.id, req).subscribe({
      next: () => {
        this.toast.success('Completed', '');
        this.loadTasks(this.selectedIntern()!.id);
      },
      error: () => this.toast.error('Update failed', '')
    });
  }

  // ========================
  // DELETE TASK
  // ========================
  deleteTask(task: InternTask) {
    this.intSvc.deleteTask(task.id).subscribe({
      next: () => {
        this.toast.success('Deleted', '');
        this.loadTasks(this.selectedIntern()!.id);
      },
      error: () => this.toast.error('Delete failed', '')
    });
  }

  // ========================
  // STATUS STYLE (FIXED)
  // ========================
  statusClass(status: any) {
    return status == 2
      ? 'zbadge-approved'
      : status == 1
      ? 'zbadge-info'
      : 'zbadge-pending';
  }

  // ========================
  // STATUS TEXT (NEW)
  // ========================
  statusText(status: any) {
    return status == 0 ? 'Pending'
         : status == 1 ? 'In Progress'
         : 'Completed';
  }
}