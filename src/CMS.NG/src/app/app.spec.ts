import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';

import { App } from './app';

describe('App shell', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
      ],
    }).compileComponents();
  });

  it('creates the shell', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the 系統管理 Admin group with the 角色 AppRole entry', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('系統管理 Admin');
    expect(text).toContain('角色 AppRole');
  });

  it('links the AppRole menu entry to /app-roles', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('a.app-nav-item');
    expect(link.getAttribute('href')).toBe('/app-roles');
  });

  it('collapses and expands the sidebar', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const shell = () => fixture.nativeElement.querySelector('.app-shell');
    const toggle: HTMLButtonElement = fixture.nativeElement.querySelector('.app-sidebar__toggle');

    expect(shell().classList).not.toContain('app-shell--collapsed');

    toggle.click();
    fixture.detectChanges();
    expect(shell().classList).toContain('app-shell--collapsed');

    toggle.click();
    fixture.detectChanges();
    expect(shell().classList).not.toContain('app-shell--collapsed');
  });

  it('collapses a nav group, hiding its items', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const groupHeader: HTMLButtonElement =
      fixture.nativeElement.querySelector('.app-nav-group__header');

    expect(fixture.nativeElement.querySelectorAll('a.app-nav-item').length).toBe(1);

    groupHeader.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('a.app-nav-item').length).toBe(0);
  });
});
