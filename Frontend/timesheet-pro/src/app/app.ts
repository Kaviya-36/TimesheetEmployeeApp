import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SpinnerComponent } from './components/spinner/spinner.component';
import { ToastComponent }   from './components/toast/toast.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, SpinnerComponent, ToastComponent],
  template: `
    <app-spinner />
    <app-toast />
    <router-outlet />
  `
})
export class App {}
