import { AppUserLookup } from './app-user.model';

/** 角色 AppRole — response model, mirrors CMS.API `AppRole`. */
export interface AppRole {
  /** 主代碼 */
  pkid: number;
  /** 角色代碼 — business key */
  roleId: string;
  /** 角色名稱 */
  roleName: string;
  /** 權限等級 */
  permissionLevel: number;
  /** 描述 */
  description: string | null;
  /** 使用者數 */
  userCount: number;
  /** 使用者 — populated on the detail read only */
  users: AppUserLookup[];
}

/** 角色 AppRole — write DTO. `roleId` is the key: editable on add, read-only on edit. */
export interface AppRoleRequest {
  roleId: string;
  roleName: string;
  permissionLevel: number;
  description: string | null;
  userIds: string[];
}

/** 角色 AppRole — search DTO. */
export interface AppRoleQuery {
  keyword?: string | null;
  permissionLevelFrom?: number | null;
  permissionLevelTo?: number | null;
}

/** An AppRoleQuery with every filter cleared. */
export const EMPTY_APP_ROLE_QUERY: AppRoleQuery = {
  keyword: null,
  permissionLevelFrom: null,
  permissionLevelTo: null,
};
