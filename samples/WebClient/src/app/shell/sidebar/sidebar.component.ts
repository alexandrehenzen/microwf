import {
  Component,
  HostBinding
} from '@angular/core';

import { AuthService } from '../../shared/services/auth.service';

@Component({
  selector: 'tw-sidebar',
  template: `
  <div class="sidebar__header">
    <div (click)="toggle()">{{ userName }}</div>
  </div>
  <div class="sidebar__content">
    <ul>
      <li routerLinkActive="active" class="menu__item">
        <a routerLink="/home">
          <tw-icon name="home"></tw-icon>
          Home
        </a>
      </li>
      <tw-menu></tw-menu>
      <li *ngIf="isAdmin">
        <span>
          <tw-icon name="wrench"></tw-icon>
          Admin
          <ul>
            <li routerLinkActive="active" class="menu__item">
              <a routerLink="/admin">
                <tw-icon name="arrow-right"></tw-icon>
                Workflows
              </a>
            </li>
          </ul>
        </span>
      </li>
      <li routerLinkActive="active" class="menu__item">
        <a href="javascript:void(0)" (click)="logout()">
          <tw-icon name="sign-out"></tw-icon>
          Log out
        </a>
      </li>
    </ul>
  </div>
  <div class="sidebar__footer"></div>
  `
})
export class SidebarComponent {
  public collapsed = false;

  @HostBinding('class')
  public classlist = this.getClassList();

  public get userName(): string {
    return this._authService.username;
  }

  public get isAdmin(): boolean {
    return this._authService.hasClaim('workflow_admin');
  }

  public constructor(
    private _authService: AuthService
  ) { }

  public logout(): void {
    this._authService.logout();
  }

  public toggle(): void {
    this.collapsed = !this.collapsed;
    this.classlist = this.getClassList();
  }

  private getClassList(): string {
    if (this.collapsed) {
      return 'sidebar sidebar--collapsed';
    }

    return 'sidebar';
  }
}
