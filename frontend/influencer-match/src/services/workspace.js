import api from './api';

export async function fetchWorkspace() {
  const { data } = await api.get('/workspace');
  return data;
}

export async function createWorkspace(name) {
  const { data } = await api.post('/workspace', { name });
  return data;
}

export async function inviteWorkspaceMember(payload) {
  const { data } = await api.post('/workspace/invites', payload);
  return data;
}

export async function acceptWorkspaceInvite(inviteToken) {
  const { data } = await api.post('/workspace/invites/accept', { inviteToken });
  return data;
}

export async function fetchMyWorkspaceInvites() {
  const { data } = await api.get('/workspace/my-invites');
  return data;
}

export async function updateWorkspaceMemberRole(workspaceMemberId, role) {
  const { data } = await api.patch(`/workspace/members/${workspaceMemberId}/role`, { role });
  return data;
}

export async function removeWorkspaceMember(workspaceMemberId) {
  await api.delete(`/workspace/members/${workspaceMemberId}`);
}
