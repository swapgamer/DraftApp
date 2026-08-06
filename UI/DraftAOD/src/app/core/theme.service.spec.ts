import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  let service: ThemeService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    service = TestBed.inject(ThemeService);
  });

  it('defaults to dark mode', () => {
    expect(service.theme()).toBe('dark');
  });

  it('persists a selected light theme', () => {
    service.set('light');
    expect(service.theme()).toBe('light');
    expect(localStorage.getItem('draft-datastore.theme')).toBe('light');
  });
});
