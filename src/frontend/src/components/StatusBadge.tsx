export type FileStatus = 'Uploaded' | 'Processing' | 'Processed' | 'Rejected';

const statusConfig: Record<FileStatus, { label: string; tone: string; icon: string }> = {
  Uploaded: { label: 'Uploaded', tone: 'badge--info', icon: '⬆' },
  Processing: { label: 'Processing', tone: 'badge--warning', icon: '⏳' },
  Processed: { label: 'Processed', tone: 'badge--success', icon: '✓' },
  Rejected: { label: 'Rejected', tone: 'badge--danger', icon: '✗' },
};

type StatusBadgeProps = {
  status: FileStatus;
};

export default function StatusBadge({ status }: StatusBadgeProps) {
  const { label, tone, icon } = statusConfig[status];
  return (
    <span className={`status-badge ${tone}`} aria-label={`Status: ${label}`}>
      <span className="sr-only">Status: {label}</span>
      <span className="status-badge__icon" aria-hidden="true">
        {icon}
      </span>
      <span className="status-badge__label">{label}</span>
    </span>
  );
}
