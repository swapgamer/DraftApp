import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PlayerService } from './services.player';
import { environment } from '../../environments/environment';

describe('PlayerService', () => {
  let service: PlayerService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [PlayerService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PlayerService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('omits empty parameters when searching', () => {
    service.search({ pageNumber: 1, pageSize: 12, search: '', sortBy: 'rank' }).subscribe();
    const request = http.expectOne(`${environment.apiUrl}/players?pageNumber=1&pageSize=12&sortBy=rank`);
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], pageNumber: 1, pageSize: 12, totalCount: 0 });
  });

  it('updates state and clears loading after a successful load', () => {
    service.load({ pageNumber: 1, pageSize: 12, sortBy: 'rank' });
    expect(service.loading()).toBeTrue();
    const request = http.expectOne(`${environment.apiUrl}/players?pageNumber=1&pageSize=12&sortBy=rank`);
    request.flush({ items: [{ id: 'p1', fullName: 'Test Player' }], pageNumber: 1, pageSize: 12, totalCount: 1 });
    expect(service.loading()).toBeFalse();
    expect(service.total()).toBe(1);
    expect(service.players()[0].fullName).toBe('Test Player');
  });

  it('clears loading after a failed load', () => {
    service.load({ pageNumber: 1, pageSize: 12, sortBy: 'rank' });
    const request = http.expectOne(`${environment.apiUrl}/players?pageNumber=1&pageSize=12&sortBy=rank`);
    request.flush('Unavailable', { status: 503, statusText: 'Unavailable' });
    expect(service.loading()).toBeFalse();
  });
});
