import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
export interface Lookup { id:number; name:string; code?:string; isoCode?:string; startYear?:number; endYear?:number; }
@Injectable({providedIn:'root'}) export class LookupsService {
 private readonly http=inject(HttpClient);
 positions(){return this.http.get<Lookup[]>(`${environment.apiUrl}/lookups/positions`);}
 nationalities(){return this.http.get<Lookup[]>(`${environment.apiUrl}/lookups/nationalities`);}
 eras(){return this.http.get<Lookup[]>(`${environment.apiUrl}/lookups/eras`);}
 eraBuckets(){return this.http.get<Lookup[]>(`${environment.apiUrl}/lookups/era-buckets`);}
}
