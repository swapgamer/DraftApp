export interface Player { id:string; fullName:string; nationality:string; playingEra:string; positions:string[]; aliases:string[]; shortDescription:string; overallRank:number; goalCreditPoints:number; assistCreditPoints:number; defensiveCreditPoints:number; transfermarktUrl?:string; wikipediaUrl?:string; primaryImageUrl?:string; }
export interface PagedResult<T> { items:T[]; pageNumber:number; pageSize:number; totalCount:number; }
export interface PlayerSearch { pageNumber:number; pageSize:number; search?:string; positionId?:number; nationalityId?:number; playingEraId?:number; activeFromYear?:number; activeToYear?:number; rank?:number; sortBy?:string; descending?:boolean; }

